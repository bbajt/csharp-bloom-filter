namespace ByTech.BloomFilter.DependencyInjection;

/// <summary>
/// Fluent configuration for registering named Bloom filters with the DI container.
/// </summary>
public sealed class BloomFilterRegistration
{
    internal List<(string Name, Func<IBloomFilter> Factory)> Entries { get; } = [];

    /// <summary>
    /// Registers a standard Bloom filter with the given name and configuration.
    /// </summary>
    /// <param name="name">The filter name.</param>
    /// <param name="configure">Configuration action for the builder.</param>
    /// <returns>This registration for chaining.</returns>
    public BloomFilterRegistration AddFilter(string name, Action<BloomFilterBuilderStage> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        Entries.Add((name, () =>
        {
            var stage = new BloomFilterBuilderStage();
            configure(stage);
            return stage.Build();
        }));

        return this;
    }

    /// <summary>
    /// Registers a thread-safe Bloom filter with the given name and configuration.
    /// </summary>
    /// <param name="name">The filter name.</param>
    /// <param name="configure">Configuration action for the builder.</param>
    /// <returns>This registration for chaining.</returns>
    public BloomFilterRegistration AddThreadSafeFilter(string name, Action<BloomFilterBuilderStage> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        Entries.Add((name, () =>
        {
            var stage = new BloomFilterBuilderStage();
            configure(stage);
            return stage.BuildThreadSafe();
        }));

        return this;
    }

    /// <summary>
    /// Registers a counting Bloom filter with the given name and configuration.
    /// </summary>
    /// <param name="name">The filter name.</param>
    /// <param name="configure">Configuration action for the builder.</param>
    /// <returns>This registration for chaining.</returns>
    public BloomFilterRegistration AddCountingFilter(string name, Action<BloomFilterBuilderStage> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        Entries.Add((name, () =>
        {
            var stage = new BloomFilterBuilderStage();
            configure(stage);
            return stage.BuildCounting();
        }));

        return this;
    }
}
