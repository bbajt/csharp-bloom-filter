namespace ByTech.BloomFilter.Diagnostics;

/// <summary>
/// Point-in-time diagnostic snapshot of a Bloom filter's state.
/// Useful for monitoring saturation and estimating current false positive rate.
/// </summary>
public sealed class BloomFilterSnapshot
{
    /// <summary>Number of items the filter was designed to hold.</summary>
    public long ExpectedInsertions { get; init; }

    /// <summary>Target false positive probability the filter was configured for.</summary>
    public double TargetFalsePositiveRate { get; init; }

    /// <summary>Total number of bits in the filter.</summary>
    public long BitCount { get; init; }

    /// <summary>Number of hash functions used per insertion.</summary>
    public int HashFunctionCount { get; init; }

    /// <summary>Number of bits currently set to 1.</summary>
    public long BitsSet { get; init; }

    /// <summary>
    /// Fraction of bits set (BitsSet / BitCount). Indicates saturation level.
    /// A value approaching 1.0 means the filter is heavily saturated.
    /// </summary>
    public double FillRatio { get; init; }

    /// <summary>
    /// Estimated false positive rate based on current saturation.
    /// Uses the formula: p ≈ (BitsSet / BitCount) ^ k.
    /// </summary>
    public double EstimatedCurrentFalsePositiveRate { get; init; }

    /// <summary>Total memory used by the bit array in bytes.</summary>
    public long MemoryBytes { get; init; }
}
