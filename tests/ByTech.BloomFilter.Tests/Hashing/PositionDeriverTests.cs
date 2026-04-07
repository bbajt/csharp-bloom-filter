using ByTech.BloomFilter.Hashing;
using FluentAssertions;
using Xunit;

namespace ByTech.BloomFilter.Tests.Hashing;

public class PositionDeriverTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Positions_are_within_bit_count_range()
    {
        const long bitCount = 10_000;
        const int k = 7;
        Span<long> positions = stackalloc long[k];

        PositionDeriver.Derive(12345UL, 67890UL, bitCount, positions, k);

        for (var i = 0; i < k; i++)
        {
            positions[i].Should().BeGreaterThanOrEqualTo(0);
            positions[i].Should().BeLessThan(bitCount);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Same_inputs_produce_same_positions()
    {
        const long bitCount = 10_000;
        const int k = 5;
        Span<long> pos1 = stackalloc long[k];
        Span<long> pos2 = stackalloc long[k];

        PositionDeriver.Derive(111UL, 222UL, bitCount, pos1, k);
        PositionDeriver.Derive(111UL, 222UL, bitCount, pos2, k);

        for (var i = 0; i < k; i++)
        {
            pos1[i].Should().Be(pos2[i]);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Different_h1_h2_produce_different_positions()
    {
        const long bitCount = 1_000_000;
        const int k = 7;
        Span<long> pos1 = stackalloc long[k];
        Span<long> pos2 = stackalloc long[k];

        PositionDeriver.Derive(100UL, 200UL, bitCount, pos1, k);
        PositionDeriver.Derive(300UL, 400UL, bitCount, pos2, k);

        // At least some positions should differ
        var anyDiff = false;
        for (var i = 0; i < k; i++)
        {
            if (pos1[i] != pos2[i]) anyDiff = true;
        }

        anyDiff.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void K_equals_1_produces_single_position()
    {
        Span<long> positions = stackalloc long[1];
        PositionDeriver.Derive(999UL, 888UL, 1000, positions, 1);

        positions[0].Should().BeGreaterThanOrEqualTo(0);
        positions[0].Should().BeLessThan(1000);
    }
}
