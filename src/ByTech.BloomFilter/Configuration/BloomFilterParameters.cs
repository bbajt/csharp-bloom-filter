namespace ByTech.BloomFilter.Configuration;

/// <summary>
/// Computed Bloom filter parameters derived from <see cref="BloomFilterOptions"/>.
/// These are the concrete values used to construct a filter instance.
/// </summary>
public sealed class BloomFilterParameters
{
    /// <summary>Number of items the filter is designed to hold.</summary>
    public long ExpectedInsertions { get; }

    /// <summary>Target false positive probability the user requested.</summary>
    public double TargetFalsePositiveRate { get; }

    /// <summary>Total number of bits in the filter.</summary>
    public long BitCount { get; }

    /// <summary>Number of 64-bit words backing the bit array.</summary>
    public int WordCount { get; }

    /// <summary>Number of hash functions (bit positions set per insertion).</summary>
    public int HashFunctionCount { get; }

    /// <summary>
    /// Estimated false positive rate given the computed parameters.
    /// May differ from <see cref="TargetFalsePositiveRate"/> if constraints were applied.
    /// </summary>
    public double EstimatedFalsePositiveRate { get; }

    /// <summary>
    /// Creates a parameter set. Intended to be constructed only by <see cref="BloomFilterCalculator"/>.
    /// </summary>
    internal BloomFilterParameters(
        long expectedInsertions,
        double targetFalsePositiveRate,
        long bitCount,
        int wordCount,
        int hashFunctionCount,
        double estimatedFalsePositiveRate)
    {
        ExpectedInsertions = expectedInsertions;
        TargetFalsePositiveRate = targetFalsePositiveRate;
        BitCount = bitCount;
        WordCount = wordCount;
        HashFunctionCount = hashFunctionCount;
        EstimatedFalsePositiveRate = estimatedFalsePositiveRate;
    }
}
