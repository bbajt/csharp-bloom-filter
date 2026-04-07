namespace ByTech.BloomFilter.Hashing;

/// <summary>
/// Derives k bit positions from two 64-bit hash values using the double-hashing scheme:
/// position(i) = (h1 + i * h2) mod m, for i = 0, 1, ..., k-1.
/// </summary>
/// <remarks>
/// Allocation-free: positions are written into a caller-supplied span.
/// </remarks>
internal static class PositionDeriver
{
    /// <summary>
    /// Generates <paramref name="k"/> bit positions from the double-hashing formula.
    /// </summary>
    /// <param name="h1">First hash value.</param>
    /// <param name="h2">Second hash value.</param>
    /// <param name="bitCount">Total number of bits in the filter (m).</param>
    /// <param name="positions">Span to receive the computed positions. Must have length >= k.</param>
    /// <param name="k">Number of positions to generate.</param>
    public static void Derive(ulong h1, ulong h2, long bitCount, Span<long> positions, int k)
    {
        var m = (ulong)bitCount;
        for (var i = 0; i < k; i++)
        {
            // Combined = h1 + i * h2 (wrapping arithmetic in ulong)
            var combined = h1 + (ulong)i * h2;
            positions[i] = (long)(combined % m);
        }
    }
}
