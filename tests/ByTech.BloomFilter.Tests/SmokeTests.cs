using FluentAssertions;
using Xunit;

namespace ByTech.BloomFilter.Tests;

public class SmokeTests
{
    [Fact]
    public void BloomFilter_can_be_constructed_via_builder()
    {
        var filter = BloomFilterBuilder
            .ForExpectedInsertions(1000)
            .WithFalsePositiveRate(0.01)
            .Build();

        filter.Should().NotBeNull();
        filter.ExpectedInsertions.Should().Be(1000);
    }
}
