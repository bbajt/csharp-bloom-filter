using ByTech.BloomFilter.Configuration;
using FluentAssertions;
using Xunit;

namespace ByTech.BloomFilter.Tests.Configuration;

public class BloomFilterCalculatorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Standard_parameters_for_1M_items_at_1_percent()
    {
        // Well-known reference: n=1M, p=0.01 → m≈9,585,059, k≈7
        var options = new BloomFilterOptions(1_000_000, 0.01);
        var parameters = BloomFilterCalculator.Compute(options);

        parameters.ExpectedInsertions.Should().Be(1_000_000);
        parameters.TargetFalsePositiveRate.Should().Be(0.01);

        // m should be approximately 9.58M (exact depends on ceiling)
        parameters.BitCount.Should().BeInRange(9_585_000, 9_586_000);

        // k should be approximately 7
        parameters.HashFunctionCount.Should().Be(7);

        // Word count = ceil(m / 64)
        parameters.WordCount.Should().Be((int)((parameters.BitCount + 63) / 64));

        // Estimated FPR should be close to target
        parameters.EstimatedFalsePositiveRate.Should().BeApproximately(0.01, 0.002);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Standard_parameters_for_10K_items_at_0_1_percent()
    {
        // n=10K, p=0.001 → m≈143,776, k≈10
        var options = new BloomFilterOptions(10_000, 0.001);
        var parameters = BloomFilterCalculator.Compute(options);

        parameters.BitCount.Should().BeInRange(143_000, 144_000);
        parameters.HashFunctionCount.Should().Be(10);
        parameters.EstimatedFalsePositiveRate.Should().BeLessThan(0.002);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Small_filter_n_equals_1()
    {
        var options = new BloomFilterOptions(1, 0.5);
        var parameters = BloomFilterCalculator.Compute(options);

        parameters.BitCount.Should().BeGreaterThanOrEqualTo(1);
        parameters.HashFunctionCount.Should().BeGreaterThanOrEqualTo(1);
        parameters.WordCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Very_small_fpr_produces_large_filter()
    {
        var options = new BloomFilterOptions(100_000, 0.0000001);
        var parameters = BloomFilterCalculator.Compute(options);

        // With p=1e-7, filter should be much larger than at p=0.01
        parameters.BitCount.Should().BeGreaterThan(1_000_000);
        parameters.HashFunctionCount.Should().BeGreaterThan(15);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Max_bit_count_constraint_is_applied()
    {
        var options = new BloomFilterOptions(1_000_000, 0.01, maxBitCount: 1_000_000);
        var parameters = BloomFilterCalculator.Compute(options);

        parameters.BitCount.Should().BeLessThanOrEqualTo(1_000_000);

        // FPR will be worse than target since we constrained bits
        parameters.EstimatedFalsePositiveRate.Should().BeGreaterThan(0.01);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Max_hash_functions_constraint_is_applied()
    {
        var options = new BloomFilterOptions(1_000_000, 0.01, maxHashFunctions: 3);
        var parameters = BloomFilterCalculator.Compute(options);

        parameters.HashFunctionCount.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Hash_function_count_is_at_least_1()
    {
        // Very high p means very few hash functions needed
        var options = new BloomFilterOptions(1_000_000, 0.99);
        var parameters = BloomFilterCalculator.Compute(options);

        parameters.HashFunctionCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Rejects_parameters_exceeding_max_supported_capacity()
    {
        // Very large n with very small p — should exceed max supported bits
        var options = new BloomFilterOptions(long.MaxValue / 2, 0.0000001);
        var act = () => BloomFilterCalculator.Compute(options);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Extremely_small_fpr_with_large_n_exceeds_capacity_and_throws()
    {
        // double.Epsilon with a large n produces rawBitCount far exceeding MaxSupportedBitCount.
        // Options accepts this combination, but the calculator must reject it.
        var options = new BloomFilterOptions(1_000_000_000, double.Epsilon);
        var act = () => BloomFilterCalculator.Compute(options);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Word_count_is_ceiling_division_by_64()
    {
        var options = new BloomFilterOptions(100, 0.01);
        var parameters = BloomFilterCalculator.Compute(options);

        var expectedWordCount = (int)((parameters.BitCount + 63) / 64);
        parameters.WordCount.Should().Be(expectedWordCount);
    }

    [Theory]
    [InlineData(64, 1000, 7, true)]   // Exact 64-bit boundary
    [InlineData(65, 1000, 7, true)]   // Just over
    [InlineData(128, 1000, 7, true)]  // Two words
    [InlineData(1, 1, 1, true)]       // Minimum
    [InlineData(0, 1000, 7, false)]   // Invalid → returns 1.0
    [Trait("Category", "Unit")]
    public void EstimateFalsePositiveRate_produces_valid_results(
        long bitCount, long n, int k, bool shouldBeLessThan1)
    {
        var fpr = BloomFilterCalculator.EstimateFalsePositiveRate(bitCount, n, k);

        if (shouldBeLessThan1)
        {
            fpr.Should().BeInRange(0.0, 1.0);
        }
        else
        {
            fpr.Should().Be(1.0);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Estimated_fpr_decreases_with_more_bits()
    {
        var fpr1 = BloomFilterCalculator.EstimateFalsePositiveRate(10_000, 1000, 7);
        var fpr2 = BloomFilterCalculator.EstimateFalsePositiveRate(100_000, 1000, 7);

        fpr2.Should().BeLessThan(fpr1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Large_n_within_safe_range_succeeds()
    {
        // 100M items at 1% — should be within limits
        var options = new BloomFilterOptions(100_000_000, 0.01);
        var parameters = BloomFilterCalculator.Compute(options);

        parameters.BitCount.Should().BeGreaterThan(0);
        parameters.HashFunctionCount.Should().BeGreaterThan(0);
    }
}
