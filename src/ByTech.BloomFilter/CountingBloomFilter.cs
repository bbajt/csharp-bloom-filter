using System.Text;
using ByTech.BloomFilter.Configuration;
using ByTech.BloomFilter.Diagnostics;
using ByTech.BloomFilter.Hashing;
using ByTech.BloomFilter.Storage;

namespace ByTech.BloomFilter;

/// <summary>
/// A counting Bloom filter that supports deletion via 4-bit counters per position.
/// Supports Add, Remove, MayContain, and Clear.
/// </summary>
/// <remarks>
/// <para>Uses 4x the memory of a standard Bloom filter (4 bits per position vs 1 bit).</para>
/// <para>Counters saturate at 15 — items hashing to saturated positions cannot be removed.</para>
/// <para>This type is not thread-safe.</para>
/// </remarks>
public sealed class CountingBloomFilter : IBloomFilter
{
    private const int StackAllocThreshold = 512;

    private readonly CountingBitStore _store;

    /// <summary>Number of items the filter is designed to hold.</summary>
    public long ExpectedInsertions { get; }

    /// <summary>Target false positive probability.</summary>
    public double TargetFalsePositiveRate { get; }

    /// <summary>Total number of counter positions in the filter.</summary>
    public long PositionCount { get; }

    /// <summary>Total number of bit positions (alias for <see cref="PositionCount"/>, satisfies <see cref="IBloomFilter"/>).</summary>
    public long BitCount => PositionCount;

    /// <summary>Number of hash functions (positions per insertion).</summary>
    public int HashFunctionCount { get; }

    /// <summary>Estimated false positive rate at design time.</summary>
    public double EstimatedFalsePositiveRate { get; }

    /// <summary>
    /// Creates a counting Bloom filter from pre-computed parameters.
    /// </summary>
    internal CountingBloomFilter(BloomFilterParameters parameters)
    {
        ExpectedInsertions = parameters.ExpectedInsertions;
        TargetFalsePositiveRate = parameters.TargetFalsePositiveRate;
        PositionCount = parameters.BitCount;
        HashFunctionCount = parameters.HashFunctionCount;
        EstimatedFalsePositiveRate = parameters.EstimatedFalsePositiveRate;
        _store = new CountingBitStore(parameters.BitCount);
    }

    /// <summary>
    /// Adds an item by incrementing counters at k positions.
    /// </summary>
    /// <param name="value">The item bytes to add.</param>
    public void Add(ReadOnlySpan<byte> value)
    {
        HashProvider.Hash(value, out var h1, out var h2);
        var k = HashFunctionCount;
        Span<long> positions = stackalloc long[k];
        PositionDeriver.Derive(h1, h2, PositionCount, positions, k);

        for (var i = 0; i < k; i++)
        {
            _store.Increment(positions[i]);
        }

        BloomFilterEventSource.Instance.ItemAdded();
    }

    /// <summary>
    /// Removes an item by decrementing counters at k positions.
    /// Returns <c>false</c> if any counter was already 0 or saturated at 15.
    /// </summary>
    /// <param name="value">The item bytes to remove.</param>
    /// <returns><c>true</c> if all k counters were successfully decremented; <c>false</c> otherwise.</returns>
    public bool Remove(ReadOnlySpan<byte> value)
    {
        HashProvider.Hash(value, out var h1, out var h2);
        var k = HashFunctionCount;
        Span<long> positions = stackalloc long[k];
        PositionDeriver.Derive(h1, h2, PositionCount, positions, k);

        var allSucceeded = true;
        for (var i = 0; i < k; i++)
        {
            if (!_store.Decrement(positions[i]))
            {
                allSucceeded = false;
            }
        }

        return allSucceeded;
    }

    /// <summary>
    /// Tests whether an item may have been added (all k counters &gt; 0).
    /// </summary>
    /// <param name="value">The item bytes to test.</param>
    /// <returns><c>true</c> if possibly present; <c>false</c> if definitely absent.</returns>
    public bool MayContain(ReadOnlySpan<byte> value)
    {
        BloomFilterEventSource.Instance.QueryPerformed();
        HashProvider.Hash(value, out var h1, out var h2);
        var k = HashFunctionCount;
        Span<long> positions = stackalloc long[k];
        PositionDeriver.Derive(h1, h2, PositionCount, positions, k);

        for (var i = 0; i < k; i++)
        {
            if (!_store.IsSet(positions[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Adds multiple items to the filter.
    /// </summary>
    public void AddRange(ReadOnlyMemory<byte>[] items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
        {
            Add(item.Span);
        }
    }

    /// <summary>
    /// Tests whether all items may have been added. Short-circuits on first definite absence.
    /// </summary>
    public bool ContainsAll(ReadOnlyMemory<byte>[] items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
        {
            if (!MayContain(item.Span))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Tests whether at least one item may have been added. Short-circuits on first possible match.
    /// </summary>
    public bool ContainsAny(ReadOnlyMemory<byte>[] items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
        {
            if (MayContain(item.Span))
                return true;
        }
        return false;
    }

    /// <summary>Adds a string item (UTF-8 encoded).</summary>
    public void Add(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        WithUtf8(value, static (span, self) => { self.Add(span); return true; });
    }

    /// <summary>Removes a string item (UTF-8 encoded).</summary>
    public bool Remove(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return WithUtf8(value, static (span, self) => self.Remove(span));
    }

    /// <summary>Tests whether a string item may have been added.</summary>
    public bool MayContain(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return WithUtf8(value, static (span, self) => self.MayContain(span));
    }

    /// <summary>Resets all counters to zero.</summary>
    public void Clear()
    {
        _store.Clear();
    }

    /// <summary>Returns a diagnostic snapshot.</summary>
    public BloomFilterSnapshot Snapshot()
    {
        var nonZero = _store.PopCountNonZero();
        var fillRatio = PositionCount > 0 ? (double)nonZero / PositionCount : 0.0;
        var estimatedCurrentFpr = Math.Pow(fillRatio, HashFunctionCount);

        return new BloomFilterSnapshot
        {
            ExpectedInsertions = ExpectedInsertions,
            TargetFalsePositiveRate = TargetFalsePositiveRate,
            BitCount = PositionCount,
            HashFunctionCount = HashFunctionCount,
            BitsSet = nonZero,
            FillRatio = fillRatio,
            EstimatedCurrentFalsePositiveRate = estimatedCurrentFpr,
            MemoryBytes = _store.ByteCount,
        };
    }

    private TResult WithUtf8<TResult>(string value, Func<ReadOnlySpan<byte>, CountingBloomFilter, TResult> action)
    {
        var maxByteCount = Encoding.UTF8.GetMaxByteCount(value.Length);

        if (maxByteCount <= StackAllocThreshold)
        {
            Span<byte> buffer = stackalloc byte[maxByteCount];
            var written = Encoding.UTF8.GetBytes(value.AsSpan(), buffer);
            return action(buffer[..written], this);
        }
        else
        {
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(maxByteCount);
            try
            {
                var written = Encoding.UTF8.GetBytes(value.AsSpan(), buffer);
                return action(buffer.AsSpan(0, written), this);
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
