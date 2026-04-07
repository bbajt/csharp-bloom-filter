using System.Text;
using ByTech.BloomFilter.Configuration;
using ByTech.BloomFilter.Diagnostics;
using ByTech.BloomFilter.Hashing;
using ByTech.BloomFilter.Storage;

namespace ByTech.BloomFilter;

/// <summary>
/// A thread-safe Bloom filter using lock-free atomic bit operations for Add
/// and MayContain, with exclusive locking for <see cref="Clear"/>.
/// </summary>
/// <remarks>
/// <para>Concurrent <c>Add</c> calls are lock-free (uses <c>Interlocked.Or</c>).</para>
/// <para>Concurrent <c>MayContain</c> calls are safe (read-only).</para>
/// <para>Concurrent <c>Add</c> + <c>MayContain</c> is safe (monotonic bit-setting).</para>
/// <para><c>Clear</c> acquires an exclusive write lock — blocks all other operations.</para>
/// </remarks>
public sealed class ThreadSafeBloomFilter : IBloomFilter, IDisposable
{
    private const int StackAllocThreshold = 512;

    private readonly ConcurrentBitStore _store;
    private readonly ReaderWriterLockSlim _lock = new();
    private volatile bool _disposed;

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
    /// </summary>
    public double EstimatedFalsePositiveRate { get; }

    /// <summary>
    /// Creates a thread-safe Bloom filter from pre-computed parameters.
    /// Use <see cref="BloomFilterBuilder.BuildThreadSafe"/> for the preferred construction path.
    /// </summary>
    internal ThreadSafeBloomFilter(BloomFilterParameters parameters)
    {
        ExpectedInsertions = parameters.ExpectedInsertions;
        TargetFalsePositiveRate = parameters.TargetFalsePositiveRate;
        BitCount = parameters.BitCount;
        HashFunctionCount = parameters.HashFunctionCount;
        EstimatedFalsePositiveRate = parameters.EstimatedFalsePositiveRate;
        _store = new ConcurrentBitStore(parameters.BitCount);
    }

    /// <summary>
    /// Adds an item to the filter. Lock-free — safe for concurrent calls.
    /// </summary>
    /// <param name="value">The item bytes to add.</param>
    public void Add(ReadOnlySpan<byte> value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        HashProvider.Hash(value, out var h1, out var h2);
        var k = HashFunctionCount;
        Span<long> positions = stackalloc long[k];
        PositionDeriver.Derive(h1, h2, BitCount, positions, k);

        _lock.EnterReadLock();
        try
        {
            for (var i = 0; i < k; i++)
            {
                _store.SetBit(positions[i]);
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }

        BloomFilterEventSource.Instance.ItemAdded();
    }

    /// <summary>
    /// Tests whether an item may have been added. Safe for concurrent calls.
    /// </summary>
    /// <param name="value">The item bytes to test.</param>
    /// <returns><c>true</c> if possibly present; <c>false</c> if definitely absent.</returns>
    public bool MayContain(ReadOnlySpan<byte> value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        HashProvider.Hash(value, out var h1, out var h2);
        var k = HashFunctionCount;
        Span<long> positions = stackalloc long[k];
        PositionDeriver.Derive(h1, h2, BitCount, positions, k);

        BloomFilterEventSource.Instance.QueryPerformed();

        _lock.EnterReadLock();
        try
        {
            for (var i = 0; i < k; i++)
            {
                if (!_store.GetBit(positions[i]))
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Adds a string item. Lock-free for concurrent calls.
    /// </summary>
    public void Add(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        WithUtf8(value, static (span, self) => { self.Add(span); return true; });
    }

    /// <summary>
    /// Tests whether a string item may have been added. Safe for concurrent calls.
    /// </summary>
    public bool MayContain(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return WithUtf8(value, static (span, self) => self.MayContain(span));
    }

    /// <summary>
    /// Adds multiple items to the filter. Each item is added under a read lock.
    /// </summary>
    public void AddRange(ReadOnlyMemory<byte>[] items)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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
        ObjectDisposedException.ThrowIf(_disposed, this);
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
        {
            if (MayContain(item.Span))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Resets all bits. Acquires exclusive write lock — blocks all other operations.
    /// </summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _lock.EnterWriteLock();
        try
        {
            _store.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Returns a point-in-time diagnostic snapshot. Values may be approximate under concurrent writes.
    /// </summary>
    public BloomFilterSnapshot Snapshot()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _lock.EnterReadLock();
        try
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
                MemoryBytes = _store.WordCount * sizeof(long),
            };
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>Disposes the internal <see cref="ReaderWriterLockSlim"/>.</summary>
    public void Dispose()
    {
        _disposed = true;
        _lock.Dispose();
    }

    /// <summary>UTF-8 encode a string and invoke the callback with the span.</summary>
    private TResult WithUtf8<TResult>(string value, Func<ReadOnlySpan<byte>, ThreadSafeBloomFilter, TResult> action)
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
