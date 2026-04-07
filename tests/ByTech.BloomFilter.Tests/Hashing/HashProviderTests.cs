using ByTech.BloomFilter.Hashing;
using FluentAssertions;
using Xunit;

namespace ByTech.BloomFilter.Tests.Hashing;

public class HashProviderTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Same_input_produces_same_hash()
    {
        var input = "hello"u8;

        HashProvider.Hash(input, out var h1a, out var h2a);
        HashProvider.Hash(input, out var h1b, out var h2b);

        h1a.Should().Be(h1b);
        h2a.Should().Be(h2b);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Different_inputs_produce_different_hashes()
    {
        HashProvider.Hash("hello"u8, out var h1a, out var h2a);
        HashProvider.Hash("world"u8, out var h1b, out var h2b);

        // Extremely unlikely both h1 and h2 collide for different inputs
        (h1a == h1b && h2a == h2b).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Empty_input_produces_valid_hash()
    {
        HashProvider.Hash(ReadOnlySpan<byte>.Empty, out var h1, out var h2);

        // Should produce some deterministic value, not crash
        (h1 != 0 || h2 != 0).Should().BeTrue("empty input should still produce a hash");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Single_byte_inputs_produce_different_hashes()
    {
        HashProvider.Hash(new byte[] { 0 }, out var h1a, out var h2a);
        HashProvider.Hash(new byte[] { 1 }, out var h1b, out var h2b);

        (h1a == h1b && h2a == h2b).Should().BeFalse();
    }
}
