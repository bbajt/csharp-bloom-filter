namespace ByTech.BloomFilter;

/// <summary>
/// Defines how a typed key is projected to bytes for Bloom filter operations.
/// Implement this interface to use the typed Add and MayContain extension methods
/// on <see cref="BloomFilterExtensions"/>.
/// </summary>
/// <typeparam name="T">The key type.</typeparam>
public interface IBloomFilterKeySerializer<in T>
{
    /// <summary>
    /// Returns the maximum number of bytes needed to serialize <paramref name="value"/>.
    /// Used to size the stack or pooled buffer.
    /// </summary>
    /// <param name="value">The value to measure.</param>
    /// <returns>Maximum byte count.</returns>
    int GetMaxByteCount(T value);

    /// <summary>
    /// Serializes <paramref name="value"/> into the destination span.
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <param name="destination">Buffer to write into. Length is at least <see cref="GetMaxByteCount"/>.</param>
    /// <returns>Number of bytes actually written.</returns>
    int Serialize(T value, Span<byte> destination);
}
