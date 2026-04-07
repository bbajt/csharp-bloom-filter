namespace ByTech.BloomFilter.Configuration;

/// <summary>
/// Computes optimal Bloom filter parameters from user-supplied options.
/// Uses standard formulas: m = -(n * ln(p)) / (ln(2)^2), k = (m / n) * ln(2).
/// </summary>
public static class BloomFilterCalculator
{
    /// <summary>ln(2) cached to avoid recomputation.</summary>
    private const double Ln2 = 0.6931471805599453;

    /// <summary>ln(2)^2 cached.</summary>
    private const double Ln2Squared = 0.4804530139182015;

    /// <summary>
    /// Maximum number of bits the library will allocate.
    /// Set to 2^37 (~16 GB) — a practical upper bound to prevent accidental OOM.
    /// </summary>
    public const long MaxSupportedBitCount = 1L << 37;

    /// <summary>
    /// Computes optimal Bloom filter parameters from the given options.
    /// </summary>
    /// <param name="options">Validated user options specifying expected insertions and target FPR.</param>
    /// <returns>Computed parameters ready to construct a filter.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the computed bit count exceeds the maximum supported capacity
    /// and no <see cref="BloomFilterOptions.MaxBitCount"/> constraint was provided.
    /// </exception>
    public static BloomFilterParameters Compute(BloomFilterOptions options)
    {
        // m = -(n * ln(p)) / (ln(2)^2)
        var rawBitCount = -(options.ExpectedInsertions * Math.Log(options.FalsePositiveRate)) / Ln2Squared;

        // Guard against double overflow before casting to long
        if (double.IsInfinity(rawBitCount) || double.IsNaN(rawBitCount) || rawBitCount > MaxSupportedBitCount)
        {
            throw new ArgumentException(
                $"Computed bit count exceeds the maximum supported capacity ({MaxSupportedBitCount:N0}). " +
                "Reduce expected insertions, increase target false positive rate, or set a MaxBitCount constraint.",
                nameof(options));
        }

        // Ceiling to ensure we don't undershoot the target FPR
        var bitCount = (long)Math.Ceiling(rawBitCount);

        // Apply maximum bit count constraint
        if (options.MaxBitCount.HasValue && bitCount > options.MaxBitCount.Value)
        {
            bitCount = options.MaxBitCount.Value;
        }

        // Hard safety limit
        if (bitCount > MaxSupportedBitCount)
        {
            throw new ArgumentException(
                $"Computed bit count ({bitCount:N0}) exceeds the maximum supported capacity ({MaxSupportedBitCount:N0}). " +
                "Reduce expected insertions, increase target false positive rate, or set a MaxBitCount constraint.",
                nameof(options));
        }

        // Ensure at least 1 bit
        if (bitCount < 1)
        {
            bitCount = 1;
        }

        // k = (m / n) * ln(2)
        var rawHashCount = ((double)bitCount / options.ExpectedInsertions) * Ln2;

        // Round to nearest, minimum 1
        var hashFunctionCount = Math.Max(1, (int)Math.Round(rawHashCount));

        // Apply maximum hash function constraint
        if (options.MaxHashFunctions.HasValue && hashFunctionCount > options.MaxHashFunctions.Value)
        {
            hashFunctionCount = options.MaxHashFunctions.Value;
        }

        // Word count: ceiling division by 64 (safe to cast — MaxSupportedBitCount ensures int range)
        var wordCount = (int)((bitCount + 63) / 64);

        // Estimated FPR with actual parameters: p ≈ (1 - e^(-kn/m))^k
        var estimatedFpr = EstimateFalsePositiveRate(bitCount, options.ExpectedInsertions, hashFunctionCount);

        return new BloomFilterParameters(
            expectedInsertions: options.ExpectedInsertions,
            targetFalsePositiveRate: options.FalsePositiveRate,
            bitCount: bitCount,
            wordCount: wordCount,
            hashFunctionCount: hashFunctionCount,
            estimatedFalsePositiveRate: estimatedFpr);
    }

    /// <summary>
    /// Estimates the false positive rate for given parameters using the standard formula:
    /// p ≈ (1 - e^(-kn/m))^k
    /// </summary>
    /// <param name="bitCount">Number of bits in the filter.</param>
    /// <param name="expectedInsertions">Expected number of items inserted.</param>
    /// <param name="hashFunctionCount">Number of hash functions.</param>
    /// <returns>Estimated false positive probability.</returns>
    public static double EstimateFalsePositiveRate(long bitCount, long expectedInsertions, int hashFunctionCount)
    {
        if (bitCount <= 0 || expectedInsertions <= 0 || hashFunctionCount <= 0)
        {
            return 1.0;
        }

        // p ≈ (1 - e^(-kn/m))^k
        var exponent = -(double)hashFunctionCount * expectedInsertions / bitCount;
        var baseVal = 1.0 - Math.Exp(exponent);
        return Math.Pow(baseVal, hashFunctionCount);
    }
}
