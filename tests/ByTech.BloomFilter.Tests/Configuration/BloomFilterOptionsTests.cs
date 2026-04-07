using ByTech.BloomFilter.Configuration;
using FluentAssertions;
using Xunit;

namespace ByTech.BloomFilter.Tests.Configuration;

public class BloomFilterOptionsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Valid_options_are_accepted()
    {
        var options = new BloomFilterOptions(1_000_000, 0.01);

        options.ExpectedInsertions.Should().Be(1_000_000);
        options.FalsePositiveRate.Should().Be(0.01);
        options.MaxBitCount.Should().BeNull();
        options.MaxHashFunctions.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Valid_options_with_constraints_are_accepted()
    {
        var options = new BloomFilterOptions(1_000, 0.05, maxBitCount: 50_000, maxHashFunctions: 5);

        options.MaxBitCount.Should().Be(50_000);
        options.MaxHashFunctions.Should().Be(5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(long.MinValue)]
    [Trait("Category", "Unit")]
    public void Rejects_non_positive_expected_insertions(long n)
    {
        var act = () => new BloomFilterOptions(n, 0.01);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("expectedInsertions");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.01)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [Trait("Category", "Unit")]
    public void Rejects_invalid_false_positive_rate(double p)
    {
        var act = () => new BloomFilterOptions(1000, p);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("falsePositiveRate");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Category", "Unit")]
    public void Rejects_non_positive_max_bit_count(long maxBits)
    {
        var act = () => new BloomFilterOptions(1000, 0.01, maxBitCount: maxBits);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxBitCount");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Category", "Unit")]
    public void Rejects_non_positive_max_hash_functions(int maxK)
    {
        var act = () => new BloomFilterOptions(1000, 0.01, maxHashFunctions: maxK);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxHashFunctions");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Accepts_boundary_values()
    {
        // Minimum valid n
        var opt1 = new BloomFilterOptions(1, 0.5);
        opt1.ExpectedInsertions.Should().Be(1);

        // Very small p
        var opt2 = new BloomFilterOptions(100, double.Epsilon);
        opt2.FalsePositiveRate.Should().Be(double.Epsilon);

        // p just under 1
        var opt3 = new BloomFilterOptions(100, 0.9999999);
        opt3.FalsePositiveRate.Should().BeApproximately(0.9999999, 1e-10);
    }
}
