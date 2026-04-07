using ByTech.BloomFilter.Configuration;

namespace ByTech.BloomFilter;

/// <summary>
/// Fluent builder for constructing <see cref="BloomFilter"/> instances
/// from desired characteristics (expected insertions and target false positive rate).
/// </summary>
public sealed class BloomFilterBuilder
{
    private long _expectedInsertions;
    private double _falsePositiveRate;
    private long? _maxBitCount;
    private int? _maxHashFunctions;

    private BloomFilterBuilder(long expectedInsertions)
    {
        _expectedInsertions = expectedInsertions;
    }

    /// <summary>
    /// Starts building a Bloom filter for the given expected number of insertions.
    /// </summary>
    /// <param name="expectedInsertions">Expected number of items to insert. Must be &gt; 0.</param>
    /// <returns>Builder instance for further configuration.</returns>
    public static BloomFilterBuilder ForExpectedInsertions(long expectedInsertions)
    {
        return new BloomFilterBuilder(expectedInsertions);
    }

    /// <summary>
    /// Sets the target false positive probability.
    /// </summary>
    /// <param name="rate">Desired false positive rate in (0, 1). For example, 0.01 for 1%.</param>
    /// <returns>This builder instance.</returns>
    public BloomFilterBuilder WithFalsePositiveRate(double rate)
    {
        _falsePositiveRate = rate;
        return this;
    }

    /// <summary>
    /// Sets an optional upper bound on the total number of bits the filter may allocate.
    /// </summary>
    /// <param name="maxBits">Maximum bit count. Must be &gt; 0 when specified.</param>
    /// <returns>This builder instance.</returns>
    public BloomFilterBuilder WithMaxBitCount(long maxBits)
    {
        _maxBitCount = maxBits;
        return this;
    }

    /// <summary>
    /// Sets an optional upper bound on the number of hash functions.
    /// </summary>
    /// <param name="maxK">Maximum hash function count. Must be &gt; 0 when specified.</param>
    /// <returns>This builder instance.</returns>
    public BloomFilterBuilder WithMaxHashFunctions(int maxK)
    {
        _maxHashFunctions = maxK;
        return this;
    }

    /// <summary>
    /// Validates all options, computes optimal parameters, and constructs the Bloom filter.
    /// </summary>
    /// <returns>A ready-to-use <see cref="BloomFilter"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">When options are invalid.</exception>
    /// <exception cref="ArgumentException">When computed parameters exceed supported limits.</exception>
    public BloomFilter Build()
    {
        var options = new BloomFilterOptions(
            _expectedInsertions,
            _falsePositiveRate,
            _maxBitCount,
            _maxHashFunctions);

        var parameters = BloomFilterCalculator.Compute(options);

        return new BloomFilter(parameters);
    }
}
