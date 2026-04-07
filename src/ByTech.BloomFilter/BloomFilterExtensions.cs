namespace ByTech.BloomFilter;

/// <summary>
/// Extension methods for adding and querying typed keys via <see cref="IBloomFilterKeySerializer{T}"/>.
/// </summary>
public static class BloomFilterExtensions
{
    private const int StackAllocThreshold = 512;

    /// <summary>
    /// Adds a typed key to the filter using the provided serializer.
    /// </summary>
    /// <typeparam name="T">The key type.</typeparam>
    /// <param name="filter">The Bloom filter.</param>
    /// <param name="value">The key to add.</param>
    /// <param name="serializer">Serializer that converts <typeparamref name="T"/> to bytes.</param>
    public static void Add<T>(this BloomFilter filter, T value, IBloomFilterKeySerializer<T> serializer)
    {
        var maxBytes = serializer.GetMaxByteCount(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        if (maxBytes <= StackAllocThreshold)
        {
            Span<byte> buffer = stackalloc byte[maxBytes];
            var written = serializer.Serialize(value, buffer);
            filter.Add(buffer[..written]);
        }
        else
        {
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(maxBytes);
            try
            {
                var written = serializer.Serialize(value, buffer);
                filter.Add(buffer.AsSpan(0, written));
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    /// <summary>
    /// Tests whether a typed key may have been added, using the provided serializer.
    /// </summary>
    /// <typeparam name="T">The key type.</typeparam>
    /// <param name="filter">The Bloom filter.</param>
    /// <param name="value">The key to test.</param>
    /// <param name="serializer">Serializer that converts <typeparamref name="T"/> to bytes.</param>
    /// <returns><c>true</c> if possibly present; <c>false</c> if definitely absent.</returns>
    public static bool MayContain<T>(this BloomFilter filter, T value, IBloomFilterKeySerializer<T> serializer)
    {
        var maxBytes = serializer.GetMaxByteCount(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        if (maxBytes <= StackAllocThreshold)
        {
            Span<byte> buffer = stackalloc byte[maxBytes];
            var written = serializer.Serialize(value, buffer);
            return filter.MayContain(buffer[..written]);
        }
        else
        {
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(maxBytes);
            try
            {
                var written = serializer.Serialize(value, buffer);
                return filter.MayContain(buffer.AsSpan(0, written));
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    /// <summary>
    /// Adds a typed key to the thread-safe filter using the provided serializer.
    /// </summary>
    public static void Add<T>(this ThreadSafeBloomFilter filter, T value, IBloomFilterKeySerializer<T> serializer)
    {
        var maxBytes = serializer.GetMaxByteCount(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        if (maxBytes <= StackAllocThreshold)
        {
            Span<byte> buffer = stackalloc byte[maxBytes];
            var written = serializer.Serialize(value, buffer);
            filter.Add(buffer[..written]);
        }
        else
        {
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(maxBytes);
            try
            {
                var written = serializer.Serialize(value, buffer);
                filter.Add(buffer.AsSpan(0, written));
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    /// <summary>
    /// Tests whether a typed key may have been added to the thread-safe filter.
    /// </summary>
    public static bool MayContain<T>(this ThreadSafeBloomFilter filter, T value, IBloomFilterKeySerializer<T> serializer)
    {
        var maxBytes = serializer.GetMaxByteCount(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        if (maxBytes <= StackAllocThreshold)
        {
            Span<byte> buffer = stackalloc byte[maxBytes];
            var written = serializer.Serialize(value, buffer);
            return filter.MayContain(buffer[..written]);
        }
        else
        {
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(maxBytes);
            try
            {
                var written = serializer.Serialize(value, buffer);
                return filter.MayContain(buffer.AsSpan(0, written));
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
