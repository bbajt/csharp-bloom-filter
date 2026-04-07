using ByTech.BloomFilter.Configuration;

namespace ByTech.BloomFilter;

/// <summary>
/// Fluent builder for constructing <see cref="CountingBloomFilter"/> instances.
/// </summary>
public sealed class CountingBloomFilterBuilder
{
    private long _expectedInsertions;
    private double _falsePositiveRate;
    private long? _maxPositionCount;
    private int? _maxHashFunctions;

    private CountingBloomFilterBuilder(long expectedInsertions)
    {
        _expectedInsertions = expectedInsertions;
    }

    /// <summary>Starts building a counting Bloom filter for the given expected insertions.</summary>
    public static CountingBloomFilterBuilder ForExpectedInsertions(long expectedInsertions)
    {
        return new CountingBloomFilterBuilder(expectedInsertions);
    }

    /// <summary>Sets the target false positive probability.</summary>
    public CountingBloomFilterBuilder WithFalsePositiveRate(double rate)
    {
        _falsePositiveRate = rate;
        return this;
    }

    /// <summary>Sets an optional upper bound on position count.</summary>
    public CountingBloomFilterBuilder WithMaxPositionCount(long maxPositions)
    {
        _maxPositionCount = maxPositions;
        return this;
    }

    /// <summary>Sets an optional upper bound on hash function count.</summary>
    public CountingBloomFilterBuilder WithMaxHashFunctions(int maxK)
    {
        _maxHashFunctions = maxK;
        return this;
    }

    /// <summary>Validates options, computes parameters, and constructs the counting filter.</summary>
    public CountingBloomFilter Build()
    {
        var options = new BloomFilterOptions(
            _expectedInsertions,
            _falsePositiveRate,
            _maxPositionCount,
            _maxHashFunctions);

        var parameters = BloomFilterCalculator.Compute(options);
        return new CountingBloomFilter(parameters);
    }
}
