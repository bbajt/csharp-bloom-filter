using System.Diagnostics.CodeAnalysis;

namespace ByTech.BloomFilter;

/// <summary>
/// Factory for retrieving named <see cref="IBloomFilter"/> instances.
/// </summary>
public interface IBloomFilterFactory
{
    /// <summary>
    /// Gets the Bloom filter registered with the specified name.
    /// </summary>
    /// <param name="name">The filter name.</param>
    /// <returns>The registered filter.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no filter is registered with the given name.</exception>
    IBloomFilter Get(string name);

    /// <summary>
    /// Attempts to get the Bloom filter registered with the specified name.
    /// </summary>
    /// <param name="name">The filter name.</param>
    /// <param name="filter">The registered filter, or <c>null</c> if not found.</param>
    /// <returns><c>true</c> if a filter was found; <c>false</c> otherwise.</returns>
    bool TryGet(string name, [MaybeNullWhen(false)] out IBloomFilter filter);
}
