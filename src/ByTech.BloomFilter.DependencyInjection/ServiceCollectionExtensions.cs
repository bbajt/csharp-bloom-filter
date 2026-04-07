using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ByTech.BloomFilter.DependencyInjection;

/// <summary>
/// Extension methods for registering Bloom filters with <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers named Bloom filters and an <see cref="IBloomFilterFactory"/> singleton with the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action to register named filters.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddBloomFilter(bf =>
    /// {
    ///     bf.AddFilter("users", b => b.WithExpectedInsertions(1_000_000).WithFalsePositiveRate(0.01));
    ///     bf.AddThreadSafeFilter("sessions", b => b.WithExpectedInsertions(500_000).WithFalsePositiveRate(0.001));
    ///     bf.AddCountingFilter("temp-keys", b => b.WithExpectedInsertions(10_000).WithFalsePositiveRate(0.05));
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddBloomFilter(this IServiceCollection services, Action<BloomFilterRegistration> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var registration = new BloomFilterRegistration();
        configure(registration);

        var factory = new BloomFilterFactory();
        foreach (var (name, filterFactory) in registration.Entries)
        {
            factory.Register(name, filterFactory());
        }

        services.TryAddSingleton<IBloomFilterFactory>(factory);

        return services;
    }
}
