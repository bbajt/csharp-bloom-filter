namespace ByTech.BloomFilter.Storage;

/// <summary>
/// Packed 4-bit counter array for counting Bloom filters.
/// Each position holds a counter in [0, 15]. Two counters are packed per byte.
/// Counters saturate at 15 (no wrap) — saturated positions cannot be decremented.
/// </summary>
internal sealed class CountingBitStore
{
    /// <summary>Maximum counter value (4-bit nibble).</summary>
    private const byte MaxCounter = 15;

    private readonly byte[] _counters;

    /// <summary>Total number of counter positions.</summary>
    public long PositionCount { get; }

    /// <summary>Number of bytes backing the counter array.</summary>
    public int ByteCount => _counters.Length;

    /// <summary>
    /// Creates a new counting store with all counters initially zero.
    /// </summary>
    /// <param name="positionCount">Number of counter positions. Must be &gt; 0.</param>
    public CountingBitStore(long positionCount)
    {
        if (positionCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(positionCount), positionCount,
                "Position count must be greater than zero.");
        }

        PositionCount = positionCount;
        // 2 counters per byte, ceiling division
        _counters = new byte[(int)((positionCount + 1) / 2)];
    }

    /// <summary>
    /// Creates a counting store from existing data (deserialization).
    /// </summary>
    internal CountingBitStore(long positionCount, byte[] counters)
    {
        ArgumentNullException.ThrowIfNull(counters);

        if (positionCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(positionCount), positionCount,
                "Position count must be greater than zero.");
        }

        var expectedLength = (int)((positionCount + 1) / 2);
        if (counters.Length != expectedLength)
        {
            throw new ArgumentException(
                $"Counter array length ({counters.Length}) does not match expected ({expectedLength}) for {positionCount} positions.",
                nameof(counters));
        }

        PositionCount = positionCount;
        _counters = counters;
    }

    /// <summary>
    /// Increments the counter at the given position. Saturates at 15.
    /// </summary>
    /// <param name="index">Zero-based position index.</param>
    public void Increment(long index)
    {
        var byteIndex = (int)(index >> 1);
        var isHighNibble = (index & 1) != 0;

        if (isHighNibble)
        {
            var current = (byte)((_counters[byteIndex] >> 4) & 0x0F);
            if (current < MaxCounter)
            {
                _counters[byteIndex] += 0x10; // increment high nibble
            }
        }
        else
        {
            var current = (byte)(_counters[byteIndex] & 0x0F);
            if (current < MaxCounter)
            {
                _counters[byteIndex]++;
            }
        }
    }

    /// <summary>
    /// Decrements the counter at the given position. Returns false if the counter
    /// was already 0 or is saturated at 15 (sticky — cannot be decremented).
    /// </summary>
    /// <param name="index">Zero-based position index.</param>
    /// <returns><c>true</c> if decremented successfully; <c>false</c> if at 0 or saturated.</returns>
    public bool Decrement(long index)
    {
        var byteIndex = (int)(index >> 1);
        var isHighNibble = (index & 1) != 0;

        if (isHighNibble)
        {
            var current = (byte)((_counters[byteIndex] >> 4) & 0x0F);
            if (current == 0 || current == MaxCounter)
            {
                return false;
            }

            _counters[byteIndex] -= 0x10;
            return true;
        }
        else
        {
            var current = (byte)(_counters[byteIndex] & 0x0F);
            if (current == 0 || current == MaxCounter)
            {
                return false;
            }

            _counters[byteIndex]--;
            return true;
        }
    }

    /// <summary>
    /// Tests whether the counter at the given position is greater than zero.
    /// </summary>
    /// <param name="index">Zero-based position index.</param>
    /// <returns><c>true</c> if the counter is &gt; 0.</returns>
    public bool IsSet(long index)
    {
        var byteIndex = (int)(index >> 1);
        var isHighNibble = (index & 1) != 0;

        if (isHighNibble)
        {
            return (_counters[byteIndex] & 0xF0) != 0;
        }

        return (_counters[byteIndex] & 0x0F) != 0;
    }

    /// <summary>
    /// Returns the counter value at the given position (0-15).
    /// </summary>
    internal int GetCounter(long index)
    {
        var byteIndex = (int)(index >> 1);
        var isHighNibble = (index & 1) != 0;
        return isHighNibble ? (_counters[byteIndex] >> 4) & 0x0F : _counters[byteIndex] & 0x0F;
    }

    /// <summary>
    /// Resets all counters to zero.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_counters);
    }

    /// <summary>
    /// Returns the number of positions with counter &gt; 0.
    /// </summary>
    public long PopCountNonZero()
    {
        var count = 0L;
        for (var i = 0L; i < PositionCount; i++)
        {
            if (IsSet(i))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Read-only access to backing data for serialization.</summary>
    public ReadOnlySpan<byte> GetData() => _counters.AsSpan();
}
