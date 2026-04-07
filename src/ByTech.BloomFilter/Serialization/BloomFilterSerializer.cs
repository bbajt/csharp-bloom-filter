using System.Buffers.Binary;
using System.IO.Hashing;
using ByTech.BloomFilter.Configuration;
using ByTech.BloomFilter.Storage;

namespace ByTech.BloomFilter.Serialization;

/// <summary>
/// Serializes and deserializes <see cref="BloomFilter"/> instances using a versioned binary format.
/// Format: header (version, parameters, hash algorithm ID) + word data + CRC32 checksum.
/// </summary>
public static class BloomFilterSerializer
{
    /// <summary>Current format version.</summary>
    private const int FormatVersion = 1;

    /// <summary>
    /// Magic bytes identifying a ByTech.BloomFilter binary payload: "BTBF" in ASCII.
    /// </summary>
    private static ReadOnlySpan<byte> MagicBytes => "BTBF"u8;

    /// <summary>
    /// Hash algorithm identifier for XxHash128 (the only supported algorithm in v1).
    /// </summary>
    private const int HashAlgorithmXxHash128 = 1;

    /// <summary>
    /// Maximum hash function count accepted during deserialization.
    /// Prevents stack overflow from stackalloc in Add/MayContain hot path.
    /// </summary>
    private const int MaxDeserializedHashFunctions = 128;

    /// <summary>
    /// Header size in bytes: 4 (magic) + 4 (version) + 8 (n) + 8 (p) + 8 (m) + 4 (k) + 4 (hash algo) = 40.
    /// </summary>
    private const int HeaderSize = 40;

    /// <summary>
    /// Writes a Bloom filter to a stream in versioned binary format.
    /// </summary>
    /// <param name="filter">The filter to serialize.</param>
    /// <param name="stream">The output stream. Must be writable.</param>
    /// <exception cref="ArgumentNullException">When filter or stream is null.</exception>
    public static void WriteTo(BloomFilter filter, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(stream);

        var crc = new Crc32();

        // Header
        Span<byte> header = stackalloc byte[HeaderSize];
        MagicBytes.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], FormatVersion);
        BinaryPrimitives.WriteInt64LittleEndian(header[8..], filter.ExpectedInsertions);
        BinaryPrimitives.WriteDoubleLittleEndian(header[16..], filter.TargetFalsePositiveRate);
        BinaryPrimitives.WriteInt64LittleEndian(header[24..], filter.BitCount);
        BinaryPrimitives.WriteInt32LittleEndian(header[32..], filter.HashFunctionCount);
        BinaryPrimitives.WriteInt32LittleEndian(header[36..], HashAlgorithmXxHash128);

        stream.Write(header);
        crc.Append(header);

        // Word data — written as raw little-endian bytes.
        // Assumes little-endian host (standard for all supported .NET 10 platforms).
        var words = filter.Store.GetWords();
        var wordBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(words);
        stream.Write(wordBytes);
        crc.Append(wordBytes);

        // CRC32 checksum
        Span<byte> checksumBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(checksumBytes, crc.GetCurrentHashAsUInt32());
        stream.Write(checksumBytes);
    }

    /// <summary>
    /// Reads a Bloom filter from a stream containing versioned binary format data.
    /// Validates magic bytes, version, metadata consistency, and CRC32 checksum.
    /// </summary>
    /// <param name="stream">The input stream. Must be readable.</param>
    /// <returns>A reconstructed <see cref="BloomFilter"/> instance.</returns>
    /// <exception cref="ArgumentNullException">When stream is null.</exception>
    /// <exception cref="InvalidDataException">When the payload is corrupted, truncated, or incompatible.</exception>
    public static BloomFilter ReadFrom(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var crc = new Crc32();

        // Read header
        Span<byte> header = stackalloc byte[HeaderSize];
        ReadExact(stream, header);
        crc.Append(header);

        // Validate magic
        if (!header[..4].SequenceEqual(MagicBytes))
        {
            throw new InvalidDataException("Invalid magic bytes. Not a ByTech.BloomFilter payload.");
        }

        // Validate version
        var version = BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
        if (version != FormatVersion)
        {
            throw new InvalidDataException(
                $"Unsupported format version {version}. This reader supports version {FormatVersion}.");
        }

        // Read parameters
        var expectedInsertions = BinaryPrimitives.ReadInt64LittleEndian(header[8..]);
        var targetFpr = BinaryPrimitives.ReadDoubleLittleEndian(header[16..]);
        var bitCount = BinaryPrimitives.ReadInt64LittleEndian(header[24..]);
        var hashFunctionCount = BinaryPrimitives.ReadInt32LittleEndian(header[32..]);
        var hashAlgorithm = BinaryPrimitives.ReadInt32LittleEndian(header[36..]);

        // Validate parameters
        if (expectedInsertions <= 0)
        {
            throw new InvalidDataException($"Invalid expected insertions: {expectedInsertions}");
        }

        if (targetFpr is <= 0.0 or >= 1.0 || double.IsNaN(targetFpr) || double.IsInfinity(targetFpr))
        {
            throw new InvalidDataException($"Invalid target false positive rate: {targetFpr}");
        }

        if (bitCount <= 0 || bitCount > BloomFilterCalculator.MaxSupportedBitCount)
        {
            throw new InvalidDataException(
                $"Invalid bit count: {bitCount}. Must be in (0, {BloomFilterCalculator.MaxSupportedBitCount}].");
        }

        if (hashFunctionCount is <= 0 or > MaxDeserializedHashFunctions)
        {
            throw new InvalidDataException(
                $"Invalid hash function count: {hashFunctionCount}. Must be in (0, {MaxDeserializedHashFunctions}].");
        }

        if (hashAlgorithm != HashAlgorithmXxHash128)
        {
            throw new InvalidDataException(
                $"Unsupported hash algorithm {hashAlgorithm}. This reader supports algorithm {HashAlgorithmXxHash128} (XxHash128).");
        }

        // Calculate expected word count and read word data directly into ulong[].
        // Assumes little-endian host (standard for all supported .NET 10 platforms).
        var wordCount = (int)((bitCount + 63) / 64);
        var words = new ulong[wordCount];
        var wordBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(words.AsSpan());
        ReadExact(stream, wordBytes);
        crc.Append(wordBytes);

        // Read and validate checksum
        Span<byte> checksumBytes = stackalloc byte[4];
        ReadExact(stream, checksumBytes);

        var expectedChecksum = crc.GetCurrentHashAsUInt32();
        var actualChecksum = BinaryPrimitives.ReadUInt32LittleEndian(checksumBytes);

        if (expectedChecksum != actualChecksum)
        {
            throw new InvalidDataException(
                $"CRC32 checksum mismatch. Expected 0x{expectedChecksum:X8}, got 0x{actualChecksum:X8}. Payload may be corrupted.");
        }

        var estimatedFpr = BloomFilterCalculator.EstimateFalsePositiveRate(bitCount, expectedInsertions, hashFunctionCount);

        var parameters = new BloomFilterParameters(
            expectedInsertions: expectedInsertions,
            targetFalsePositiveRate: targetFpr,
            bitCount: bitCount,
            wordCount: wordCount,
            hashFunctionCount: hashFunctionCount,
            estimatedFalsePositiveRate: estimatedFpr);

        var store = new BitStore(bitCount, words);
        return new BloomFilter(parameters, store);
    }

    /// <summary>
    /// Reads exactly <paramref name="buffer"/>.Length bytes from the stream, or throws.
    /// </summary>
    private static void ReadExact(Stream stream, Span<byte> buffer)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = stream.Read(buffer[totalRead..]);
            if (read == 0)
            {
                throw new InvalidDataException(
                    $"Unexpected end of stream. Expected {buffer.Length} bytes, got {totalRead}.");
            }

            totalRead += read;
        }
    }
}
