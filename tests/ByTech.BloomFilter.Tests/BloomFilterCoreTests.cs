using System.Text;
using FluentAssertions;
using Xunit;

namespace ByTech.BloomFilter.Tests;

public class BloomFilterCoreTests
{
    private static BloomFilter CreateFilter(long n = 10_000, double p = 0.01)
    {
        return BloomFilterBuilder
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
    public void Non_added_item_is_likely_not_found()
    {
        var filter = CreateFilter();
        filter.Add("hello"u8);

        // This specific item should not be found (extremely unlikely false positive for single item)
        filter.MayContain("world"u8).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Multiple_added_items_are_all_found()
    {
        var filter = CreateFilter();
        var items = new[] { "alpha"u8.ToArray(), "beta"u8.ToArray(), "gamma"u8.ToArray(), "delta"u8.ToArray() };

        foreach (var item in items)
        {
            filter.Add(item);
        }

        foreach (var item in items)
        {
            filter.MayContain(item).Should().BeTrue();
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Clear_resets_filter()
    {
        var filter = CreateFilter();
        filter.Add("hello"u8);
        filter.MayContain("hello"u8).Should().BeTrue();

        filter.Clear();

        filter.MayContain("hello"u8).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void String_overload_matches_span_overload()
    {
        var filter = CreateFilter();

        filter.Add("test-string");
        filter.MayContain("test-string").Should().BeTrue();

        // Manually encode and check via span path
        var bytes = Encoding.UTF8.GetBytes("test-string");
        filter.MayContain(bytes).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void String_add_then_span_query_works()
    {
        var filter = CreateFilter();
        filter.Add("hello");

        filter.MayContain("hello"u8).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Span_add_then_string_query_works()
    {
        var filter = CreateFilter();
        filter.Add("hello"u8);

        filter.MayContain("hello").Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Empty_input_can_be_added_and_queried()
    {
        var filter = CreateFilter();
        filter.Add(ReadOnlySpan<byte>.Empty);

        filter.MayContain(ReadOnlySpan<byte>.Empty).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Snapshot_returns_correct_metrics()
    {
        var filter = CreateFilter(n: 1000, p: 0.01);

        var snap1 = filter.Snapshot();
        snap1.BitsSet.Should().Be(0);
        snap1.FillRatio.Should().Be(0.0);
        snap1.BitCount.Should().Be(filter.BitCount);
        snap1.HashFunctionCount.Should().Be(filter.HashFunctionCount);

        filter.Add("item1"u8);
        filter.Add("item2"u8);

        var snap2 = filter.Snapshot();
        snap2.BitsSet.Should().BeGreaterThan(0);
        snap2.FillRatio.Should().BeGreaterThan(0.0);
        snap2.MemoryBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Repeated_add_is_idempotent()
    {
        var filter = CreateFilter();
        filter.Add("hello"u8);
        var snap1 = filter.Snapshot();

        filter.Add("hello"u8);
        var snap2 = filter.Snapshot();

        snap1.BitsSet.Should().Be(snap2.BitsSet);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Zero_false_negatives_for_10K_items()
    {
        var filter = CreateFilter(n: 10_000, p: 0.01);
        var key = new byte[4];

        for (var i = 0; i < 10_000; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(key, i);
            filter.Add(key);
        }

        for (var i = 0; i < 10_000; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(key, i);
            filter.MayContain(key).Should().BeTrue($"item {i} was added but not found");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void False_positive_rate_is_in_sane_range()
    {
        const int n = 10_000;
        const double targetFpr = 0.01;
        var filter = CreateFilter(n: n, p: targetFpr);
        var key = new byte[4];

        // Add n items
        for (var i = 0; i < n; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(key, i);
            filter.Add(key);
        }

        // Query n absent items (offset by n so they don't overlap)
        var falsePositives = 0;
        const int queryCount = 10_000;
        for (var i = n; i < n + queryCount; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(key, i);
            if (filter.MayContain(key))
            {
                falsePositives++;
            }
        }

        var observedFpr = (double)falsePositives / queryCount;

        // Allow up to 3x the target rate — statistical variance is expected
        observedFpr.Should().BeLessThan(targetFpr * 3,
            $"observed FPR {observedFpr:P2} should be within 3x of target {targetFpr:P2}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Add_clear_query_confirms_empty()
    {
        var filter = CreateFilter(n: 1000, p: 0.01);
        var key = new byte[4];

        for (var i = 0; i < 100; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(key, i);
            filter.Add(key);
        }

        filter.Snapshot().BitsSet.Should().BeGreaterThan(0);
        filter.Clear();
        filter.Snapshot().BitsSet.Should().Be(0);

        // After clear, previously-added items should not be found
        for (var i = 0; i < 100; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(key, i);
            filter.MayContain(key).Should().BeFalse();
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Long_string_overload_works()
    {
        var filter = CreateFilter();
        // String longer than stackalloc threshold (512 bytes → ~170 chars max for ASCII)
        var longString = new string('x', 600);

        filter.Add(longString);
        filter.MayContain(longString).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Add_null_string_throws()
    {
        var filter = CreateFilter();
        var act = () => filter.Add((string)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MayContain_null_string_throws()
    {
        var filter = CreateFilter();
        var act = () => filter.MayContain((string)null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
