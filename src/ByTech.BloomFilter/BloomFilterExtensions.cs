using System.Text;

namespace ByTech.BloomFilter;

/// <summary>
/// Extension methods for adding and querying typed keys via <see cref="IBloomFilterKeySerializer{T}"/>.
/// Also provides batch string and generic overloads.
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
    public static void Add<T>(this IBloomFilter filter, T value, IBloomFilterKeySerializer<T> serializer)
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
    public static bool MayContain<T>(this IBloomFilter filter, T value, IBloomFilterKeySerializer<T> serializer)
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
    /// Adds multiple string items to the filter (UTF-8 encoded).
    /// </summary>
    /// <param name="filter">The Bloom filter.</param>
    /// <param name="values">The strings to add.</param>
    public static void AddRange(this IBloomFilter filter, IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (var value in values)
        {
            filter.Add(value);
        }
    }

    /// <summary>
    /// Tests whether all string items may have been added. Short-circuits on first definite absence.
    /// </summary>
    public static bool ContainsAll(this IBloomFilter filter, IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (var value in values)
        {
            if (!filter.MayContain(value))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Tests whether at least one string item may have been added. Short-circuits on first possible match.
    /// </summary>
    public static bool ContainsAny(this IBloomFilter filter, IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (var value in values)
        {
            if (filter.MayContain(value))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Adds multiple typed keys to the filter using the provided serializer.
    /// </summary>
    public static void AddRange<T>(this IBloomFilter filter, IEnumerable<T> values, IBloomFilterKeySerializer<T> serializer)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (var value in values)
        {
            filter.Add(value, serializer);
        }
    }

    /// <summary>
    /// Tests whether all typed keys may have been added.
    /// </summary>
    public static bool ContainsAll<T>(this IBloomFilter filter, IEnumerable<T> values, IBloomFilterKeySerializer<T> serializer)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (var value in values)
        {
            if (!filter.MayContain(value, serializer))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Tests whether at least one typed key may have been added.
    /// </summary>
    public static bool ContainsAny<T>(this IBloomFilter filter, IEnumerable<T> values, IBloomFilterKeySerializer<T> serializer)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (var value in values)
        {
            if (filter.MayContain(value, serializer))
                return true;
        }
        return false;
    }
}
