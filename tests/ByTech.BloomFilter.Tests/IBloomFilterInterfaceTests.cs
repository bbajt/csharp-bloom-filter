using System.Text;
using FluentAssertions;
using Xunit;

namespace ByTech.BloomFilter.Tests;

[Trait("Category", "Unit")]
public class IBloomFilterInterfaceTests
{
    [Fact]
    public void BloomFilter_implements_IBloomFilter()
    {
        IBloomFilter filter = BloomFilterBuilder
            .ForExpectedInsertions(1000)
            .WithFalsePositiveRate(0.01)
            .Build();

        filter.Should().BeAssignableTo<IBloomFilter>();
    }

    [Fact]
    public void ThreadSafeBloomFilter_implements_IBloomFilter()
    {
        IBloomFilter filter = BloomFilterBuilder
            .ForExpectedInsertions(1000)
            .WithFalsePositiveRate(0.01)
            .BuildThreadSafe();

        filter.Should().BeAssignableTo<IBloomFilter>();
    }

    [Fact]
    public void CountingBloomFilter_implements_IBloomFilter()
    {
        IBloomFilter filter = CountingBloomFilterBuilder
            .ForExpectedInsertions(1000)
            .WithFalsePositiveRate(0.01)
            .Build();

        filter.Should().BeAssignableTo<IBloomFilter>();
    }

    [Fact]
    public void Interface_Add_and_MayContain_span_works_through_IBloomFilter()
    {
        IBloomFilter filter = BloomFilterBuilder
            .ForExpectedInsertions(1000)
            .WithFalsePositiveRate(0.01)
            .Build();

        var key = "hello"u8;
        filter.Add(key);
        filter.MayContain(key).Should().BeTrue();
        filter.MayContain("definitely-not-present"u8).Should().BeFalse();
    }

    [Fact]
    public void Interface_Add_and_MayContain_string_works_through_IBloomFilter()
    {
        IBloomFilter filter = BloomFilterBuilder
            .ForExpectedInsertions(1000)
            .WithFalsePositiveRate(0.01)
            .Build();

        filter.Add("hello");
        filter.MayContain("hello").Should().BeTrue();
        filter.MayContain("definitely-not-present").Should().BeFalse();
    }

    [Fact]
    public void Interface_Clear_works_through_IBloomFilter()
    {
        IBloomFilter filter = BloomFilterBuilder
            .ForExpectedInsertions(1000)
            .WithFalsePositiveRate(0.01)
            .Build();

        filter.Add("hello");
        filter.Clear();
        filter.MayContain("hello").Should().BeFalse();
    }

    [Fact]
    public void Interface_properties_exposed_correctly()
    {
        IBloomFilter filter = BloomFilterBuilder
            .ForExpectedInsertions(1000)
            .WithFalsePositiveRate(0.01)
            .Build();

        filter.ExpectedInsertions.Should().Be(1000);
        filter.TargetFalsePositiveRate.Should().Be(0.01);
        filter.BitCount.Should().BeGreaterThan(0);
        filter.HashFunctionCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CountingBloomFilter_BitCount_equals_PositionCount()
    {
        var counting = CountingBloomFilterBuilder
            .ForExpectedInsertions(1000)
            .WithFalsePositiveRate(0.01)
            .Build();

        IBloomFilter asInterface = counting;
        asInterface.BitCount.Should().Be(counting.PositionCount);
    }

    [Fact]
    public void Generic_extensions_work_through_IBloomFilter()
    {
        IBloomFilter filter = BloomFilterBuilder
            .ForExpectedInsertions(1000)
            .WithFalsePositiveRate(0.01)
            .Build();

        var serializer = new IntSerializer();
        filter.Add(42, serializer);
        filter.MayContain(42, serializer).Should().BeTrue();
        filter.MayContain(99, serializer).Should().BeFalse();
    }

    [Fact]
    public void Generic_extensions_work_through_IBloomFilter_with_ThreadSafe()
    {
        IBloomFilter filter = BloomFilterBuilder
            .ForExpectedInsertions(1000)
            .WithFalsePositiveRate(0.01)
            .BuildThreadSafe();

        var serializer = new IntSerializer();
        filter.Add(42, serializer);
        filter.MayContain(42, serializer).Should().BeTrue();
    }

    [Fact]
    public void Generic_extensions_work_through_IBloomFilter_with_Counting()
    {
        IBloomFilter filter = CountingBloomFilterBuilder
            .ForExpectedInsertions(1000)
            .WithFalsePositiveRate(0.01)
            .Build();

        var serializer = new IntSerializer();
        filter.Add(42, serializer);
        filter.MayContain(42, serializer).Should().BeTrue();
    }

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
