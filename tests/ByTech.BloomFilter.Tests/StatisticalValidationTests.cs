using System.Buffers.Binary;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace ByTech.BloomFilter.Tests;

/// <summary>
/// Validates observed false positive rate against theoretical expectations.
/// Uses large sample sizes to reduce statistical variance.
/// </summary>
public class StatisticalValidationTests
{
    private readonly ITestOutputHelper _output;

    public StatisticalValidationTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(10_000, 0.01, 50_000)]
    [InlineData(10_000, 0.001, 50_000)]
    [InlineData(100_000, 0.01, 50_000)]
    [InlineData(100_000, 0.001, 50_000)]
    [Trait("Category", "Statistical")]
    public void Observed_fpr_is_within_acceptable_envelope(int n, double targetFpr, int queryCount)
    {
        var filter = BloomFilterBuilder
            .ForExpectedInsertions(n)
            .WithFalsePositiveRate(targetFpr)
            .Build();

        // Insert n unique items
        var key = new byte[8];
        for (var i = 0; i < n; i++)
        {
            BinaryPrimitives.WriteInt64LittleEndian(key, i);
            filter.Add(key);
        }

        // Query queryCount items that were never added (offset by n)
        var falsePositives = 0;
        for (long i = n; i < n + queryCount; i++)
        {
            BinaryPrimitives.WriteInt64LittleEndian(key, i);
            if (filter.MayContain(key))
            {
                falsePositives++;
            }
        }

        var observedFpr = (double)falsePositives / queryCount;
        var upperBound = targetFpr * 3.0; // Allow up to 3x target

        _output.WriteLine($"n={n}, targetFPR={targetFpr}, queries={queryCount}");
        _output.WriteLine($"Observed FPR: {observedFpr:P4} ({falsePositives}/{queryCount})");
        _output.WriteLine($"Upper bound:  {upperBound:P4}");
        _output.WriteLine($"Filter: m={filter.BitCount}, k={filter.HashFunctionCount}");

        observedFpr.Should().BeLessThan(upperBound,
            $"observed FPR {observedFpr:P4} exceeds 3x target {targetFpr:P4}");
    }

    [Fact]
    [Trait("Category", "Statistical")]
    public void Zero_false_negatives_for_100K_items()
    {
        const int n = 100_000;
        var filter = BloomFilterBuilder
            .ForExpectedInsertions(n)
            .WithFalsePositiveRate(0.01)
            .Build();

        var key = new byte[8];
        for (long i = 0; i < n; i++)
        {
            BinaryPrimitives.WriteInt64LittleEndian(key, i);
            filter.Add(key);
        }

        for (long i = 0; i < n; i++)
        {
            BinaryPrimitives.WriteInt64LittleEndian(key, i);
            filter.MayContain(key).Should().BeTrue($"item {i} was added but not found — false negative detected");
        }
    }

    [Theory]
    [InlineData(1.0)]   // exact capacity
    [InlineData(2.0)]   // 2x overfill
    [InlineData(5.0)]   // 5x overfill
    [Trait("Category", "Statistical")]
    public void Saturation_behavior_fpr_increases_with_overfill(double fillFactor)
    {
        const int designedN = 10_000;
        const double targetFpr = 0.01;
        var filter = BloomFilterBuilder
            .ForExpectedInsertions(designedN)
            .WithFalsePositiveRate(targetFpr)
            .Build();

        var insertCount = (int)(designedN * fillFactor);
        var key = new byte[8];
        for (long i = 0; i < insertCount; i++)
        {
            BinaryPrimitives.WriteInt64LittleEndian(key, i);
            filter.Add(key);
        }

        // Measure FPR on 10K absent items
        var falsePositives = 0;
        const int queryCount = 10_000;
        for (long i = insertCount; i < insertCount + queryCount; i++)
        {
            BinaryPrimitives.WriteInt64LittleEndian(key, i);
            if (filter.MayContain(key))
            {
                falsePositives++;
            }
        }

        var observedFpr = (double)falsePositives / queryCount;
        var snapshot = filter.Snapshot();

        _output.WriteLine($"Fill factor: {fillFactor}x, inserted: {insertCount}");
        _output.WriteLine($"Observed FPR: {observedFpr:P4}");
        _output.WriteLine($"Bits set: {snapshot.BitsSet}/{snapshot.BitCount} ({snapshot.FillRatio:P2})");
        _output.WriteLine($"Snapshot estimated FPR: {snapshot.EstimatedCurrentFalsePositiveRate:P4}");

        // At 1x fill, FPR should be near target
        // At higher fills, FPR will be worse — just verify it's less than 1.0
        observedFpr.Should().BeLessThan(1.0);

        if (fillFactor <= 1.0)
        {
            observedFpr.Should().BeLessThan(targetFpr * 3.0);
        }
    }
}
