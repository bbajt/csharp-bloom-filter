using System.Runtime.InteropServices;
using System.Text;
using ByTech.BloomFilter.Configuration;
using ByTech.BloomFilter.Diagnostics;
using ByTech.BloomFilter.Hashing;
using ByTech.BloomFilter.Storage;

namespace ByTech.BloomFilter;

/// <summary>
/// A high-performance, space-efficient probabilistic data structure for membership testing.
/// Returns "definitely not in set" or "possibly in set" with a configurable false positive rate.
/// </summary>
/// <remarks>
/// This type is not thread-safe. Callers must provide external synchronization
/// if the filter is shared across threads.
/// </remarks>
public sealed class BloomFilter
{
    /// <summary>Maximum string byte length for stack allocation. Longer strings use a rented array.</summary>
    private const int StackAllocThreshold = 512;

    private readonly BitStore _store;

    /// <summary>Number of items the filter is designed to hold.</summary>
    public long ExpectedInsertions { get; }

    /// <summary>Target false positive probability the filter was configured for.</summary>
    public double TargetFalsePositiveRate { get; }

    /// <summary>Total number of bits in the filter's bit array.</summary>
    public long BitCount { get; }

    /// <summary>Number of hash functions (bit positions set per insertion).</summary>
    public int HashFunctionCount { get; }

    /// <summary>
    /// Estimated false positive rate given the computed parameters at design time.
    /// For current saturation-based estimate, use <see cref="Snapshot"/>.
    /// </summary>
    public double EstimatedFalsePositiveRate { get; }

    /// <summary>
    /// Creates a Bloom filter from pre-computed parameters.
    /// Use <see cref="BloomFilterBuilder"/> for the preferred construction path.
    /// </summary>
    /// <param name="parameters">Computed parameters from <see cref="BloomFilterCalculator"/>.</param>
    internal BloomFilter(BloomFilterParameters parameters)
    {
        ExpectedInsertions = parameters.ExpectedInsertions;
        TargetFalsePositiveRate = parameters.TargetFalsePositiveRate;
        BitCount = parameters.BitCount;
        HashFunctionCount = parameters.HashFunctionCount;
        EstimatedFalsePositiveRate = parameters.EstimatedFalsePositiveRate;
        _store = new BitStore(parameters.BitCount);
    }

    /// <summary>
    /// Creates a Bloom filter from parameters and an existing bit store (used during deserialization).
    /// </summary>
    internal BloomFilter(BloomFilterParameters parameters, BitStore store)
    {
        ExpectedInsertions = parameters.ExpectedInsertions;
        TargetFalsePositiveRate = parameters.TargetFalsePositiveRate;
        BitCount = parameters.BitCount;
        HashFunctionCount = parameters.HashFunctionCount;
        EstimatedFalsePositiveRate = parameters.EstimatedFalsePositiveRate;
        _store = store;
    }

    /// <summary>
    /// Adds an item to the filter. After this call, <see cref="MayContain(ReadOnlySpan{byte})"/>
    /// will always return <c>true</c> for the same input.
    /// </summary>
    /// <param name="value">The item bytes to add.</param>
    public void Add(ReadOnlySpan<byte> value)
    {
        HashProvider.Hash(value, out var h1, out var h2);
        var k = HashFunctionCount;
        Span<long> positions = stackalloc long[k];
        PositionDeriver.Derive(h1, h2, BitCount, positions, k);

        for (var i = 0; i < k; i++)
        {
            _store.SetBit(positions[i]);
        }
    }

    /// <summary>
    /// Tests whether an item may have been added to the filter.
    /// Returns <c>false</c> if the item was definitely not added.
    /// Returns <c>true</c> if the item was possibly added (may be a false positive).
    /// </summary>
    /// <param name="value">The item bytes to test.</param>
    /// <returns><c>true</c> if possibly present; <c>false</c> if definitely absent.</returns>
    public bool MayContain(ReadOnlySpan<byte> value)
    {
        HashProvider.Hash(value, out var h1, out var h2);
        var k = HashFunctionCount;
        Span<long> positions = stackalloc long[k];
        PositionDeriver.Derive(h1, h2, BitCount, positions, k);

        for (var i = 0; i < k; i++)
        {
            if (!_store.GetBit(positions[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Adds a string item to the filter. The string is UTF-8 encoded and
    /// passed through the span-based <see cref="Add(ReadOnlySpan{byte})"/> path.
    /// </summary>
    /// <param name="value">The string to add.</param>
    public void Add(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        AddOrQuery(value, add: true);
    }

    /// <summary>
    /// Tests whether a string item may have been added to the filter.
    /// The string is UTF-8 encoded and passed through the span-based
    /// <see cref="MayContain(ReadOnlySpan{byte})"/> path.
    /// </summary>
    /// <param name="value">The string to test.</param>
    /// <returns><c>true</c> if possibly present; <c>false</c> if definitely absent.</returns>
    public bool MayContain(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return AddOrQuery(value, add: false);
    }

    /// <summary>
    /// Resets the filter to its initial empty state. All bits are cleared.
    /// </summary>
    public void Clear()
    {
        _store.Clear();
    }

    /// <summary>
    /// Returns a point-in-time diagnostic snapshot of the filter's state.
    /// </summary>
    /// <returns>A snapshot containing saturation metrics and estimated current FPR.</returns>
    public BloomFilterSnapshot Snapshot()
    {
        var bitsSet = _store.PopCount();
        var fillRatio = BitCount > 0 ? (double)bitsSet / BitCount : 0.0;
        var estimatedCurrentFpr = Math.Pow(fillRatio, HashFunctionCount);

        return new BloomFilterSnapshot
        {
            ExpectedInsertions = ExpectedInsertions,
            TargetFalsePositiveRate = TargetFalsePositiveRate,
            BitCount = BitCount,
            HashFunctionCount = HashFunctionCount,
            BitsSet = bitsSet,
            FillRatio = fillRatio,
            EstimatedCurrentFalsePositiveRate = estimatedCurrentFpr,
            MemoryBytes = _store.WordCount * sizeof(ulong),
        };
    }

    /// <summary>Provides read-only access to the backing store for serialization.</summary>
    internal BitStore Store => _store;

    /// <summary>
    /// Shared implementation for string Add and MayContain.
    /// UTF-8 encodes to stackalloc for short strings, rented array for long strings.
    /// </summary>
    private bool AddOrQuery(string value, bool add)
    {
        var maxByteCount = Encoding.UTF8.GetMaxByteCount(value.Length);

        if (maxByteCount <= StackAllocThreshold)
        {
            Span<byte> buffer = stackalloc byte[maxByteCount];
            var written = Encoding.UTF8.GetBytes(value.AsSpan(), buffer);
            var utf8 = buffer[..written];

            if (add)
            {
                Add(utf8);
                return true;
            }

            return MayContain(utf8);
        }
        else
        {
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(maxByteCount);
            try
            {
                var written = Encoding.UTF8.GetBytes(value.AsSpan(), buffer);
                var utf8 = buffer.AsSpan(0, written);

                if (add)
                {
                    Add(utf8);
                    return true;
                }

                return MayContain(utf8);
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
