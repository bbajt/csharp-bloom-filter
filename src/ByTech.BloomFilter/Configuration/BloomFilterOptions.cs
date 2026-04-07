namespace ByTech.BloomFilter.Configuration;

/// <summary>
/// Describes the desired characteristics of a Bloom filter.
/// Used as input to the parameter calculator which derives optimal bit count and hash count.
/// </summary>
public sealed class BloomFilterOptions
{
    /// <summary>
    /// Expected number of items to be inserted into the filter.
    /// Must be greater than zero.
    /// </summary>
    public long ExpectedInsertions { get; }

    /// <summary>
    /// Target false positive probability, expressed as a value in the open interval (0, 1).
    /// For example, 0.01 means a 1% expected false positive rate.
    /// </summary>
    public double FalsePositiveRate { get; }

    /// <summary>
    /// Optional upper bound on the total number of bits the filter may allocate.
    /// When set, the calculator will clamp <c>m</c> to this value, which may increase the actual false positive rate.
    /// </summary>
    public long? MaxBitCount { get; }

    /// <summary>
    /// Optional upper bound on the number of hash functions.
    /// When set, the calculator will clamp <c>k</c> to this value.
    /// </summary>
    public int? MaxHashFunctions { get; }

    /// <summary>
    /// Creates a validated set of Bloom filter options.
    /// </summary>
    /// <param name="expectedInsertions">Expected number of items to insert. Must be &gt; 0.</param>
    /// <param name="falsePositiveRate">Target false positive probability. Must be in (0, 1).</param>
    /// <param name="maxBitCount">Optional upper bound on bit count.</param>
    /// <param name="maxHashFunctions">Optional upper bound on hash function count.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is out of its valid range.</exception>
    public BloomFilterOptions(
        long expectedInsertions,
        double falsePositiveRate,
        long? maxBitCount = null,
        int? maxHashFunctions = null)
    {
        if (expectedInsertions <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedInsertions),
                expectedInsertions,
                "Expected insertions must be greater than zero.");
        }

        if (double.IsNaN(falsePositiveRate) || double.IsInfinity(falsePositiveRate) || falsePositiveRate is <= 0.0 or >= 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(falsePositiveRate),
                falsePositiveRate,
                "False positive rate must be a finite number between 0 (exclusive) and 1 (exclusive).");
        }

        if (maxBitCount is not null and <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxBitCount),
                maxBitCount,
                "Maximum bit count must be greater than zero when specified.");
        }

        if (maxHashFunctions is not null and <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxHashFunctions),
                maxHashFunctions,
                "Maximum hash function count must be greater than zero when specified.");
        }

        ExpectedInsertions = expectedInsertions;
        FalsePositiveRate = falsePositiveRate;
        MaxBitCount = maxBitCount;
        MaxHashFunctions = maxHashFunctions;
    }
}
