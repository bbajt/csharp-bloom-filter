using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ByTech.BloomFilter;

/// <summary>
/// Thread-safe registry of named <see cref="IBloomFilter"/> instances.
/// </summary>
public sealed class BloomFilterFactory : IBloomFilterFactory
{
    private readonly ConcurrentDictionary<string, IBloomFilter> _filters = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a filter with the specified name.
    /// </summary>
    /// <param name="name">The filter name.</param>
    /// <param name="filter">The filter instance.</param>
    /// <exception cref="ArgumentException">Thrown when a filter with the same name is already registered.</exception>
    public void Register(string name, IBloomFilter filter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(filter);

        if (!_filters.TryAdd(name, filter))
            throw new ArgumentException($"A filter named '{name}' is already registered.", nameof(name));
    }

    /// <inheritdoc />
    public IBloomFilter Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_filters.TryGetValue(name, out var filter))
            return filter;

        throw new KeyNotFoundException($"No filter registered with name '{name}'.");
    }

    /// <inheritdoc />
    public bool TryGet(string name, [MaybeNullWhen(false)] out IBloomFilter filter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _filters.TryGetValue(name, out filter);
    }
}
