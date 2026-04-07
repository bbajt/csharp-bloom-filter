namespace ByTech.BloomFilter.Storage;

/// <summary>
/// Packed bit array backed by <c>ulong[]</c> for high-throughput bit operations.
/// All mutating operations are allocation-free after construction.
/// </summary>
/// <remarks>
/// This type is not thread-safe. The caller must provide external synchronization
/// if the store is shared across threads.
/// </remarks>
internal sealed class BitStore
{
    private readonly ulong[] _words;

    /// <summary>Total number of bits this store was configured for.</summary>
    public long BitCount { get; }

    /// <summary>Number of 64-bit words backing the bit array.</summary>
    public int WordCount => _words.Length;

    /// <summary>
    /// Creates a new bit store with the specified number of bits.
    /// All bits are initially zero.
    /// </summary>
    /// <param name="bitCount">Total number of bits. Must be &gt; 0.</param>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="bitCount"/> is &lt;= 0.</exception>
    public BitStore(long bitCount)
    {
        if (bitCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), bitCount,
                "Bit count must be greater than zero.");
        }

        BitCount = bitCount;

        // Ceiling division: (bitCount + 63) / 64
        var wordCount = (int)((bitCount + 63) >> 6);
        _words = new ulong[wordCount];
    }

    /// <summary>
    /// Creates a bit store from an existing word array (e.g., during deserialization).
    /// </summary>
    /// <param name="bitCount">Logical bit count for this store.</param>
    /// <param name="words">Pre-populated word array. Length must match ceil(bitCount/64).</param>
    internal BitStore(long bitCount, ulong[] words)
    {
        ArgumentNullException.ThrowIfNull(words);

        if (bitCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), bitCount,
                "Bit count must be greater than zero.");
        }

        var expectedWordCount = (int)((bitCount + 63) >> 6);
        if (words.Length != expectedWordCount)
        {
            throw new ArgumentException(
                $"Word array length ({words.Length}) does not match expected word count ({expectedWordCount}) for {bitCount} bits.",
                nameof(words));
        }

        BitCount = bitCount;
        _words = words;
    }

    /// <summary>
    /// Sets the bit at the specified index to 1.
    /// No bounds checking is performed — caller must ensure index is in [0, <see cref="BitCount"/>).
    /// Out-of-range indices cause undefined behavior or <see cref="IndexOutOfRangeException"/>.
    /// </summary>
    /// <param name="index">Zero-based bit index. Caller must ensure it is in [0, <see cref="BitCount"/>).</param>
    public void SetBit(long index)
    {
        var wordIndex = (int)(index >> 6);         // index / 64
        var bitMask = 1UL << (int)(index & 63);    // index % 64
        _words[wordIndex] |= bitMask;
    }

    /// <summary>
    /// Tests whether the bit at the specified index is set to 1.
    /// No bounds checking is performed — caller must ensure index is in [0, <see cref="BitCount"/>).
    /// </summary>
    /// <param name="index">Zero-based bit index. Caller must ensure it is in [0, <see cref="BitCount"/>).</param>
    /// <returns><c>true</c> if the bit is set; <c>false</c> otherwise.</returns>
    public bool GetBit(long index)
    {
        var wordIndex = (int)(index >> 6);
        var bitMask = 1UL << (int)(index & 63);
        return (_words[wordIndex] & bitMask) != 0;
    }

    /// <summary>
    /// Resets all bits to zero.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_words);
    }

    /// <summary>
    /// Returns the total number of bits set to 1 (population count).
    /// Useful for diagnostics and saturation estimation.
    /// </summary>
    /// <returns>Number of set bits.</returns>
    public long PopCount()
    {
        var count = 0L;
        for (var i = 0; i < _words.Length; i++)
        {
            count += (long)ulong.PopCount(_words[i]);
        }

        return count;
    }

    /// <summary>
    /// Provides read-only access to the underlying word array for serialization.
    /// </summary>
    /// <returns>A read-only span over the backing <c>ulong[]</c>.</returns>
    public ReadOnlySpan<ulong> GetWords() => _words.AsSpan();

    /// <summary>
    /// Provides a writable span over the underlying word array for deserialization.
    /// </summary>
    /// <returns>A span over the backing <c>ulong[]</c>.</returns>
    internal Span<ulong> GetWordsWritable() => _words.AsSpan();
}
