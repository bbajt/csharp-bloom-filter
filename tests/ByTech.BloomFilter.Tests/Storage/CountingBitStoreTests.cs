using ByTech.BloomFilter.Storage;
using FluentAssertions;
using Xunit;

namespace ByTech.BloomFilter.Tests.Storage;

public class CountingBitStoreTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void New_store_has_all_counters_zero()
    {
        var store = new CountingBitStore(100);

        for (var i = 0L; i < 100; i++)
        {
            store.IsSet(i).Should().BeFalse();
            store.GetCounter(i).Should().Be(0);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Increment_and_IsSet_round_trip()
    {
        var store = new CountingBitStore(10);

        store.Increment(3);
        store.IsSet(3).Should().BeTrue();
        store.GetCounter(3).Should().Be(1);

        // Adjacent positions unaffected
        store.IsSet(2).Should().BeFalse();
        store.IsSet(4).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Nibble_boundary_even_odd_positions()
    {
        var store = new CountingBitStore(4);

        store.Increment(0); // low nibble of byte 0
        store.Increment(1); // high nibble of byte 0
        store.Increment(2); // low nibble of byte 1
        store.Increment(3); // high nibble of byte 1

        store.GetCounter(0).Should().Be(1);
        store.GetCounter(1).Should().Be(1);
        store.GetCounter(2).Should().Be(1);
        store.GetCounter(3).Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Increment_multiple_times()
    {
        var store = new CountingBitStore(10);

        for (var i = 0; i < 5; i++)
        {
            store.Increment(0);
        }

        store.GetCounter(0).Should().Be(5);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Counter_saturates_at_15()
    {
        var store = new CountingBitStore(10);

        for (var i = 0; i < 20; i++)
        {
            store.Increment(0);
        }

        store.GetCounter(0).Should().Be(15);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Decrement_succeeds_for_positive_counter()
    {
        var store = new CountingBitStore(10);
        store.Increment(0);
        store.Increment(0);

        store.Decrement(0).Should().BeTrue();
        store.GetCounter(0).Should().Be(1);

        store.Decrement(0).Should().BeTrue();
        store.GetCounter(0).Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Decrement_at_zero_returns_false()
    {
        var store = new CountingBitStore(10);
        store.Decrement(0).Should().BeFalse();
        store.GetCounter(0).Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Decrement_at_saturated_returns_false()
    {
        var store = new CountingBitStore(10);

        // Saturate to 15
        for (var i = 0; i < 20; i++)
        {
            store.Increment(0);
        }

        store.GetCounter(0).Should().Be(15);
        store.Decrement(0).Should().BeFalse("saturated counters are sticky");
        store.GetCounter(0).Should().Be(15);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Clear_resets_all_counters()
    {
        var store = new CountingBitStore(10);
        store.Increment(0);
        store.Increment(5);
        store.Increment(9);

        store.Clear();

        for (var i = 0L; i < 10; i++)
        {
            store.GetCounter(i).Should().Be(0);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void PopCountNonZero_counts_correctly()
    {
        var store = new CountingBitStore(10);
        store.Increment(0);
        store.Increment(3);
        store.Increment(7);

        store.PopCountNonZero().Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Category", "Unit")]
    public void Rejects_non_positive_position_count(long count)
    {
        var act = () => new CountingBitStore(count);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Odd_position_count_allocates_correct_bytes()
    {
        var store = new CountingBitStore(7); // 7 positions → 4 bytes (ceil(7/2))
        store.ByteCount.Should().Be(4);

        // All 7 positions should be usable
        for (var i = 0L; i < 7; i++)
        {
            store.Increment(i);
        }

        store.PopCountNonZero().Should().Be(7);
    }
}
