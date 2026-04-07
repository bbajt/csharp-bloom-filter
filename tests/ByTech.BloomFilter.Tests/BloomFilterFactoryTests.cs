using FluentAssertions;
using Xunit;

namespace ByTech.BloomFilter.Tests;

[Trait("Category", "Unit")]
public class BloomFilterFactoryTests
{
    private static IBloomFilter CreateFilter() =>
        BloomFilterBuilder.ForExpectedInsertions(1000).WithFalsePositiveRate(0.01).Build();

    [Fact]
    public void Register_and_Get_returns_same_instance()
    {
        var factory = new BloomFilterFactory();
        var filter = CreateFilter();

        factory.Register("users", filter);

        factory.Get("users").Should().BeSameAs(filter);
    }

    [Fact]
    public void Get_is_case_insensitive()
    {
        var factory = new BloomFilterFactory();
        var filter = CreateFilter();

        factory.Register("Users", filter);

        factory.Get("users").Should().BeSameAs(filter);
        factory.Get("USERS").Should().BeSameAs(filter);
    }

    [Fact]
    public void Get_throws_KeyNotFoundException_for_missing_name()
    {
        var factory = new BloomFilterFactory();

        var act = () => factory.Get("missing");

        act.Should().Throw<KeyNotFoundException>()
            .WithMessage("*missing*");
    }

    [Fact]
    public void TryGet_returns_true_when_found()
    {
        var factory = new BloomFilterFactory();
        var filter = CreateFilter();
        factory.Register("orders", filter);

        factory.TryGet("orders", out var result).Should().BeTrue();
        result.Should().BeSameAs(filter);
    }

    [Fact]
    public void TryGet_returns_false_when_not_found()
    {
        var factory = new BloomFilterFactory();

        factory.TryGet("missing", out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void Register_duplicate_name_throws()
    {
        var factory = new BloomFilterFactory();
        factory.Register("dup", CreateFilter());

        var act = () => factory.Register("dup", CreateFilter());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*dup*");
    }

    [Fact]
    public void Register_null_name_throws()
    {
        var factory = new BloomFilterFactory();
        var act = () => factory.Register(null!, CreateFilter());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_null_filter_throws()
    {
        var factory = new BloomFilterFactory();
        var act = () => factory.Register("test", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Register_whitespace_name_throws()
    {
        var factory = new BloomFilterFactory();
        var act = () => factory.Register("  ", CreateFilter());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Multiple_filters_registered_and_retrieved()
    {
        var factory = new BloomFilterFactory();
        var f1 = CreateFilter();
        var f2 = BloomFilterBuilder.ForExpectedInsertions(5000).WithFalsePositiveRate(0.001).Build();
        var f3 = CountingBloomFilterBuilder.ForExpectedInsertions(1000).WithFalsePositiveRate(0.01).Build();

        factory.Register("standard", f1);
        factory.Register("precise", f2);
        factory.Register("counting", f3);

        factory.Get("standard").Should().BeSameAs(f1);
        factory.Get("precise").Should().BeSameAs(f2);
        factory.Get("counting").Should().BeSameAs(f3);
    }

    [Fact]
    public void IBloomFilterFactory_interface_works()
    {
        IBloomFilterFactory factory = new BloomFilterFactory();
        var f = CreateFilter();
        ((BloomFilterFactory)factory).Register("test", f);

        factory.Get("test").Should().BeSameAs(f);
        factory.TryGet("test", out var result).Should().BeTrue();
        result.Should().BeSameAs(f);
    }
}
