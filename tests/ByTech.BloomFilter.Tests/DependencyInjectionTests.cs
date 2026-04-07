using ByTech.BloomFilter.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ByTech.BloomFilter.Tests;

[Trait("Category", "Unit")]
public class DependencyInjectionTests
{
    [Fact]
    public void AddBloomFilter_registers_IBloomFilterFactory()
    {
        var services = new ServiceCollection();

        services.AddBloomFilter(bf =>
        {
            bf.AddFilter("users", b => b.WithExpectedInsertions(1000).WithFalsePositiveRate(0.01));
        });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IBloomFilterFactory>();

        factory.Should().NotBeNull();
    }

    [Fact]
    public void Registered_filter_is_resolvable_by_name()
    {
        var provider = BuildProvider(bf =>
        {
            bf.AddFilter("users", b => b.WithExpectedInsertions(10_000).WithFalsePositiveRate(0.01));
        });

        var factory = provider.GetRequiredService<IBloomFilterFactory>();
        var filter = factory.Get("users");

        filter.Should().NotBeNull();
        filter.ExpectedInsertions.Should().Be(10_000);
        filter.TargetFalsePositiveRate.Should().Be(0.01);
    }

    [Fact]
    public void Multiple_filter_types_registered()
    {
        var provider = BuildProvider(bf =>
        {
            bf.AddFilter("standard", b => b.WithExpectedInsertions(1000).WithFalsePositiveRate(0.01));
            bf.AddThreadSafeFilter("threadsafe", b => b.WithExpectedInsertions(5000).WithFalsePositiveRate(0.001));
            bf.AddCountingFilter("counting", b => b.WithExpectedInsertions(2000).WithFalsePositiveRate(0.05));
        });

        var factory = provider.GetRequiredService<IBloomFilterFactory>();

        factory.Get("standard").Should().BeOfType<BloomFilter>();
        factory.Get("threadsafe").Should().BeOfType<ThreadSafeBloomFilter>();
        factory.Get("counting").Should().BeOfType<CountingBloomFilter>();
    }

    [Fact]
    public void Factory_is_singleton()
    {
        var provider = BuildProvider(bf =>
        {
            bf.AddFilter("test", b => b.WithExpectedInsertions(1000).WithFalsePositiveRate(0.01));
        });

        var f1 = provider.GetRequiredService<IBloomFilterFactory>();
        var f2 = provider.GetRequiredService<IBloomFilterFactory>();

        f1.Should().BeSameAs(f2);
    }

    [Fact]
    public void Filter_operations_work_through_DI()
    {
        var provider = BuildProvider(bf =>
        {
            bf.AddFilter("cache", b => b.WithExpectedInsertions(10_000).WithFalsePositiveRate(0.01));
        });

        var factory = provider.GetRequiredService<IBloomFilterFactory>();
        var filter = factory.Get("cache");

        filter.Add("hello");
        filter.MayContain("hello").Should().BeTrue();
        filter.MayContain("world").Should().BeFalse();
    }

    [Fact]
    public void TryGet_works_through_DI()
    {
        var provider = BuildProvider(bf =>
        {
            bf.AddFilter("exists", b => b.WithExpectedInsertions(1000).WithFalsePositiveRate(0.01));
        });

        var factory = provider.GetRequiredService<IBloomFilterFactory>();

        factory.TryGet("exists", out var filter).Should().BeTrue();
        filter.Should().NotBeNull();

        factory.TryGet("missing", out _).Should().BeFalse();
    }

    [Fact]
    public void Missing_filter_throws_KeyNotFoundException()
    {
        var provider = BuildProvider(bf =>
        {
            bf.AddFilter("only", b => b.WithExpectedInsertions(1000).WithFalsePositiveRate(0.01));
        });

        var factory = provider.GetRequiredService<IBloomFilterFactory>();

        var act = () => factory.Get("missing");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Default_builder_values_produce_valid_filter()
    {
        var provider = BuildProvider(bf =>
        {
            bf.AddFilter("defaults", _ => { });
        });

        var factory = provider.GetRequiredService<IBloomFilterFactory>();
        var filter = factory.Get("defaults");

        filter.ExpectedInsertions.Should().Be(1_000_000);
        filter.TargetFalsePositiveRate.Should().Be(0.01);
    }

    [Fact]
    public void AddBloomFilter_null_configure_throws()
    {
        var services = new ServiceCollection();
        var act = () => services.AddBloomFilter(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private static ServiceProvider BuildProvider(Action<BloomFilterRegistration> configure)
    {
        var services = new ServiceCollection();
        services.AddBloomFilter(configure);
        return services.BuildServiceProvider();
    }
}
