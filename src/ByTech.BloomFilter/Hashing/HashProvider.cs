using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace ByTech.BloomFilter.Hashing;

/// <summary>
/// Produces two 64-bit hash values from input bytes using XxHash128.
/// The two values are used by <see cref="PositionDeriver"/> to generate
/// k bit positions via double hashing.
/// </summary>
/// <remarks>
/// Allocation-free: uses stack-based buffers and static XxHash128 methods.
/// Thread-safe: no mutable state.
/// </remarks>
internal static class HashProvider
{
    /// <summary>
    /// Computes two 64-bit hash values from the input bytes.
    /// Uses XxHash128, splitting the 128-bit output into h1 (low 64 bits) and h2 (high 64 bits).
    /// </summary>
    /// <param name="input">The bytes to hash.</param>
    /// <param name="h1">The first 64-bit hash value (low bits).</param>
    /// <param name="h2">The second 64-bit hash value (high bits).</param>
    public static void Hash(ReadOnlySpan<byte> input, out ulong h1, out ulong h2)
    {
        // XxHash128 produces 128 bits → 16 bytes
        Span<byte> hashBytes = stackalloc byte[16];
        XxHash128.Hash(input, hashBytes);

        // Split into two 64-bit values (little-endian)
        h1 = MemoryMarshal.Read<ulong>(hashBytes);
        h2 = MemoryMarshal.Read<ulong>(hashBytes[8..]);
    }
}
