using System.Buffers.Binary;
using FluentAssertions;
using Xunit;

namespace ByTech.BloomFilter.Tests;

public class CountingBloomFilterTests
{
    private static CountingBloomFilter CreateFilter(long n = 10_000, double p = 0.01)
    {
        return CountingBloomFilterBuilder
            .ForExpectedInsertions(n)
            .WithFalsePositiveRate(p)
            .Build();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Added_item_is_found()
    {
        var filter = CreateFilter();
        filter.Add("hello"u8);
        filter.MayContain("hello"u8).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Non_added_item_is_not_found()
    {
        var filter = CreateFilter();
        filter.Add("hello"u8);
        filter.MayContain("world"u8).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Add_then_remove_then_not_found()
    {
        var filter = CreateFilter();
        filter.Add("hello"u8);
        filter.MayContain("hello"u8).Should().BeTrue();

        var removed = filter.Remove("hello"u8);
        removed.Should().BeTrue();
        filter.MayContain("hello"u8).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Add_twice_remove_once_still_found()
    {
        var filter = CreateFilter();
        filter.Add("hello"u8);
        filter.Add("hello"u8);
        filter.MayContain("hello"u8).Should().BeTrue();

        filter.Remove("hello"u8);
        filter.MayContain("hello"u8).Should().BeTrue();

        filter.Remove("hello"u8);
        filter.MayContain("hello"u8).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Remove_non_added_item_returns_false()
    {
        var filter = CreateFilter();
        var removed = filter.Remove("never-added"u8);
        removed.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void String_overloads_work()
    {
        var filter = CreateFilter();
        filter.Add("test");
        filter.MayContain("test").Should().BeTrue();

        filter.Remove("test").Should().BeTrue();
        filter.MayContain("test").Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Clear_resets_all_counters()
    {
        var filter = CreateFilter();
        filter.Add("a"u8);
        filter.Add("b"u8);
        filter.Snapshot().BitsSet.Should().BeGreaterThan(0);

        filter.Clear();
        filter.Snapshot().BitsSet.Should().Be(0);
        filter.MayContain("a"u8).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Snapshot_returns_metrics()
    {
        var filter = CreateFilter(n: 1000, p: 0.01);
        filter.Add("x"u8);

        var snap = filter.Snapshot();
        snap.BitsSet.Should().BeGreaterThan(0);
        snap.FillRatio.Should().BeGreaterThan(0);
        snap.MemoryBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Zero_false_negatives_for_1K_items()
    {
        var filter = CreateFilter(n: 1000, p: 0.01);
        var key = new byte[4];

        for (var i = 0; i < 1000; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(key, i);
            filter.Add(key);
        }

        for (var i = 0; i < 1000; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(key, i);
            filter.MayContain(key).Should().BeTrue($"item {i} not found");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Add_remove_mixed_workload()
    {
        var filter = CreateFilter(n: 1000, p: 0.01);
        var key = new byte[4];

        // Add 500 items
        for (var i = 0; i < 500; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(key, i);
            filter.Add(key);
        }

        // Remove first 200
        for (var i = 0; i < 200; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(key, i);
            filter.Remove(key).Should().BeTrue();
        }

        // Items 0-199 should not be found (may have false positives from other items' counters)
        // Items 200-499 should still be found
        for (var i = 200; i < 500; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(key, i);
            filter.MayContain(key).Should().BeTrue($"item {i} should still be present");
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Null_string_throws()
    {
        var filter = CreateFilter();
        var addAct = () => filter.Add((string)null!);
        var removeAct = () => filter.Remove((string)null!);
        var queryAct = () => filter.MayContain((string)null!);
        addAct.Should().Throw<ArgumentNullException>();
        removeAct.Should().Throw<ArgumentNullException>();
        queryAct.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Saturated_counter_prevents_removal()
    {
        var filter = CreateFilter(n: 1000, p: 0.01);

        // Add same item 20 times to saturate all k counters at 15
        for (var i = 0; i < 20; i++)
        {
            filter.Add("saturate-me"u8);
        }

        // Remove should return false (sticky saturated counters)
        filter.Remove("saturate-me"u8).Should().BeFalse();

        // Item should still be present
        filter.MayContain("saturate-me"u8).Should().BeTrue();
    }
}
