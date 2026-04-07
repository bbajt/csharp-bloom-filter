using System.Text;
using FluentAssertions;
using Xunit;

namespace ByTech.BloomFilter.Tests;

[Trait("Category", "Unit")]
public class BatchOperationsTests
{
    private static IBloomFilter CreateFilter() =>
        BloomFilterBuilder.ForExpectedInsertions(10_000).WithFalsePositiveRate(0.01).Build();

    private static IBloomFilter CreateThreadSafeFilter() =>
        BloomFilterBuilder.ForExpectedInsertions(10_000).WithFalsePositiveRate(0.01).BuildThreadSafe();

    private static IBloomFilter CreateCountingFilter() =>
        CountingBloomFilterBuilder.ForExpectedInsertions(10_000).WithFalsePositiveRate(0.01).Build();

    private static ReadOnlyMemory<byte>[] ToMemory(params string[] values) =>
        values.Select(v => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(v)).ToArray();

    // --- AddRange / ContainsAll / ContainsAny (byte[] interface methods) ---

    [Theory]
    [InlineData("Standard")]
    [InlineData("ThreadSafe")]
    [InlineData("Counting")]
    public void AddRange_then_ContainsAll_returns_true(string filterType)
    {
        var filter = CreateByType(filterType);
        var items = ToMemory("alpha", "beta", "gamma");

        filter.AddRange(items);

        filter.ContainsAll(items).Should().BeTrue();
    }

    [Theory]
    [InlineData("Standard")]
    [InlineData("ThreadSafe")]
    [InlineData("Counting")]
    public void ContainsAll_returns_false_when_item_missing(string filterType)
    {
        var filter = CreateByType(filterType);
        filter.AddRange(ToMemory("alpha", "beta"));

        filter.ContainsAll(ToMemory("alpha", "beta", "missing")).Should().BeFalse();
    }

    [Theory]
    [InlineData("Standard")]
    [InlineData("ThreadSafe")]
    [InlineData("Counting")]
    public void ContainsAny_returns_true_when_some_present(string filterType)
    {
        var filter = CreateByType(filterType);
        filter.Add("alpha");

        filter.ContainsAny(ToMemory("missing", "alpha")).Should().BeTrue();
    }

    [Theory]
    [InlineData("Standard")]
    [InlineData("ThreadSafe")]
    [InlineData("Counting")]
    public void ContainsAny_returns_false_when_none_present(string filterType)
    {
        var filter = CreateByType(filterType);

        filter.ContainsAny(ToMemory("missing1", "missing2")).Should().BeFalse();
    }

    [Theory]
    [InlineData("Standard")]
    [InlineData("ThreadSafe")]
    [InlineData("Counting")]
    public void Empty_array_ContainsAll_returns_true(string filterType)
    {
        var filter = CreateByType(filterType);
        filter.ContainsAll(Array.Empty<ReadOnlyMemory<byte>>()).Should().BeTrue();
    }

    [Theory]
    [InlineData("Standard")]
    [InlineData("ThreadSafe")]
    [InlineData("Counting")]
    public void Empty_array_ContainsAny_returns_false(string filterType)
    {
        var filter = CreateByType(filterType);
        filter.ContainsAny(Array.Empty<ReadOnlyMemory<byte>>()).Should().BeFalse();
    }

    // --- String batch extension methods ---

    [Fact]
    public void AddRange_strings_then_ContainsAll_strings()
    {
        var filter = CreateFilter();
        var items = new[] { "one", "two", "three" };

        filter.AddRange(items);

        filter.ContainsAll(items).Should().BeTrue();
        filter.ContainsAny(new[] { "missing", "one" }).Should().BeTrue();
        filter.ContainsAll(new[] { "one", "missing" }).Should().BeFalse();
    }

    [Fact]
    public void ContainsAny_strings_returns_false_when_empty()
    {
        var filter = CreateFilter();
        filter.ContainsAny(Array.Empty<string>()).Should().BeFalse();
    }

    // --- Generic T batch extension methods ---

    [Fact]
    public void AddRange_generic_then_ContainsAll_generic()
    {
        var filter = CreateFilter();
        var serializer = new IntSerializer();
        var values = new[] { 1, 2, 3, 4, 5 };

        filter.AddRange(values, serializer);

        filter.ContainsAll(values, serializer).Should().BeTrue();
        filter.ContainsAny(new[] { 99, 1 }, serializer).Should().BeTrue();
        filter.ContainsAll(new[] { 1, 99 }, serializer).Should().BeFalse();
    }

    [Fact]
    public void ContainsAny_generic_returns_false_for_empty()
    {
        var filter = CreateFilter();
        var serializer = new IntSerializer();
        filter.ContainsAny(Array.Empty<int>(), serializer).Should().BeFalse();
    }

    // --- Null argument validation ---

    [Fact]
    public void AddRange_null_throws()
    {
        var filter = CreateFilter();
        var act = () => filter.AddRange((ReadOnlyMemory<byte>[])null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ContainsAll_null_throws()
    {
        var filter = CreateFilter();
        var act = () => filter.ContainsAll((ReadOnlyMemory<byte>[])null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ContainsAny_null_throws()
    {
        var filter = CreateFilter();
        var act = () => filter.ContainsAny((ReadOnlyMemory<byte>[])null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // --- Large batch ---

    [Fact]
    public void AddRange_large_batch_no_false_negatives()
    {
        var filter = CreateFilter();
        var items = Enumerable.Range(0, 1000)
            .Select(i => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes($"item-{i}"))
            .ToArray();

        filter.AddRange(items);

        filter.ContainsAll(items).Should().BeTrue();
    }

    private static IBloomFilter CreateByType(string type) => type switch
    {
        "Standard" => CreateFilter(),
        "ThreadSafe" => CreateThreadSafeFilter(),
        "Counting" => CreateCountingFilter(),
        _ => throw new ArgumentException($"Unknown filter type: {type}")
    };

    private sealed class IntSerializer : IBloomFilterKeySerializer<int>
    {
        public int GetMaxByteCount(int value) => sizeof(int);

        public int Serialize(int value, Span<byte> destination)
        {
            BitConverter.TryWriteBytes(destination, value);
            return sizeof(int);
        }
    }
}
