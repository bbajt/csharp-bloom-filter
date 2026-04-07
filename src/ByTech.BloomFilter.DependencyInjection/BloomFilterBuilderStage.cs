namespace ByTech.BloomFilter.DependencyInjection;

/// <summary>
/// Simplified builder stage for DI registration. Captures expected insertions and FPR,
/// then delegates to the appropriate builder.
/// </summary>
public sealed class BloomFilterBuilderStage
{
    private long _expectedInsertions = 1_000_000;
    private double _falsePositiveRate = 0.01;

    /// <summary>
    /// Sets the expected number of insertions.
    /// </summary>
    public BloomFilterBuilderStage WithExpectedInsertions(long expectedInsertions)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedInsertions);
        _expectedInsertions = expectedInsertions;
        return this;
    }

    /// <summary>
    /// Sets the target false positive rate.
    /// </summary>
    public BloomFilterBuilderStage WithFalsePositiveRate(double rate)
    {
        if (double.IsNaN(rate) || double.IsInfinity(rate) || rate is <= 0.0 or >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "False positive rate must be between 0 and 1 exclusive.");
        _falsePositiveRate = rate;
        return this;
    }

    internal IBloomFilter Build() =>
        BloomFilterBuilder
            .ForExpectedInsertions(_expectedInsertions)
            .WithFalsePositiveRate(_falsePositiveRate)
            .Build();

    internal IBloomFilter BuildThreadSafe() =>
        BloomFilterBuilder
            .ForExpectedInsertions(_expectedInsertions)
            .WithFalsePositiveRate(_falsePositiveRate)
            .BuildThreadSafe();

    internal IBloomFilter BuildCounting() =>
        CountingBloomFilterBuilder
            .ForExpectedInsertions(_expectedInsertions)
            .WithFalsePositiveRate(_falsePositiveRate)
            .Build();
}
