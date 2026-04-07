using FluentAssertions;
using Xunit;

namespace ByTech.BloomFilter.Tests.Configuration;

public class BloomFilterBuilderTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Builder_produces_valid_filter()
    {
        var filter = BloomFilterBuilder
            .ForExpectedInsertions(1_000_000)
            .WithFalsePositiveRate(0.01)
            .Build();

        filter.ExpectedInsertions.Should().Be(1_000_000);
        filter.TargetFalsePositiveRate.Should().Be(0.01);
        filter.BitCount.Should().BeGreaterThan(0);
        filter.HashFunctionCount.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Builder_with_constraints_produces_constrained_filter()
    {
        var filter = BloomFilterBuilder
            .ForExpectedInsertions(1_000_000)
            .WithFalsePositiveRate(0.01)
            .WithMaxBitCount(500_000)
            .WithMaxHashFunctions(3)
            .Build();

        filter.BitCount.Should().BeLessThanOrEqualTo(500_000);
        filter.HashFunctionCount.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Builder_rejects_zero_insertions()
    {
        var act = () => BloomFilterBuilder
            .ForExpectedInsertions(0)
            .WithFalsePositiveRate(0.01)
            .Build();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Builder_rejects_invalid_fpr()
    {
        var act = () => BloomFilterBuilder
            .ForExpectedInsertions(1000)
            .WithFalsePositiveRate(0.0)
            .Build();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Builder_end_to_end_produces_correct_properties()
    {
        var filter = BloomFilterBuilder
            .ForExpectedInsertions(50_000)
            .WithFalsePositiveRate(0.001)
            .Build();

        // Verify all properties are populated and consistent
        filter.ExpectedInsertions.Should().Be(50_000);
        filter.TargetFalsePositiveRate.Should().Be(0.001);
        filter.BitCount.Should().BeGreaterThan(50_000);
        filter.HashFunctionCount.Should().BeInRange(5, 20);
        filter.EstimatedFalsePositiveRate.Should().BeInRange(0.0, 0.01);
    }
}
