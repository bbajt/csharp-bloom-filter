using System.Runtime.CompilerServices;

namespace ByTech.BloomFilter.Storage;

/// <summary>
/// Thread-safe packed bit array backed by <c>long[]</c> using <see cref="Interlocked.Or(ref long, long)"/>
/// for atomic bit-set operations. Bits only transition 0→1, making lock-free adds safe.
/// </summary>
/// <remarks>
/// <para>Concurrent <c>SetBit</c> calls are lock-free and safe.</para>
/// <para>Concurrent <c>GetBit</c> calls are safe (read-only).</para>
/// <para>Concurrent <c>SetBit</c> + <c>GetBit</c> is safe (monotonic bit-setting).</para>
/// <para><c>Clear</c> is NOT safe for concurrent use — caller must ensure exclusive access.</para>
/// <para><c>PopCount</c> may return approximate results under concurrent writes.</para>
/// </remarks>
internal sealed class ConcurrentBitStore
{
    // Stored as long[] instead of ulong[] so Interlocked.Or can be used directly
    private readonly long[] _words;

    /// <summary>Total number of bits this store was configured for.</summary>
    public long BitCount { get; }

    /// <summary>Number of 64-bit words backing the bit array.</summary>
    public int WordCount => _words.Length;

    /// <summary>
    /// Creates a new concurrent bit store with all bits initially zero.
    /// </summary>
    /// <param name="bitCount">Total number of bits. Must be &gt; 0.</param>
    public ConcurrentBitStore(long bitCount)
    {
        if (bitCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), bitCount,
                "Bit count must be greater than zero.");
        }

        BitCount = bitCount;
        var wordCount = (int)((bitCount + 63) >> 6);
        _words = new long[wordCount];
    }

    /// <summary>
    /// Atomically sets the bit at the specified index to 1 using <see cref="Interlocked.Or(ref long, long)"/>.
    /// Lock-free and safe for concurrent calls.
    /// </summary>
    /// <param name="index">Zero-based bit index. Caller must ensure it is in [0, <see cref="BitCount"/>).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBit(long index)
    {
        var wordIndex = (int)(index >> 6);
        var bitMask = 1L << (int)(index & 63);
        Interlocked.Or(ref _words[wordIndex], bitMask);
    }

    /// <summary>
    /// Tests whether the bit at the specified index is set to 1.
    /// Safe for concurrent reads, even during concurrent <see cref="SetBit"/> calls.
    /// </summary>
    /// <param name="index">Zero-based bit index. Caller must ensure it is in [0, <see cref="BitCount"/>).</param>
    /// <returns><c>true</c> if the bit is set; <c>false</c> otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetBit(long index)
    {
        var wordIndex = (int)(index >> 6);
        var bitMask = 1L << (int)(index & 63);
        return (Volatile.Read(ref _words[wordIndex]) & bitMask) != 0;
    }

    /// <summary>
    /// Resets all bits to zero. NOT safe for concurrent use — caller must ensure exclusive access.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_words);
    }

    /// <summary>
    /// Returns the total number of bits set to 1. May be approximate under concurrent writes.
    /// </summary>
    public long PopCount()
    {
        var count = 0L;
        for (var i = 0; i < _words.Length; i++)
        {
            count += long.PopCount(Volatile.Read(ref _words[i]));
        }

        return count;
    }

    /// <summary>
    /// Provides read-only access to the underlying word array for serialization.
    /// The returned span contains <c>long</c> values that should be reinterpreted as <c>ulong</c>.
    /// </summary>
    public ReadOnlySpan<long> GetWords() => _words.AsSpan();
}
