using ByTech.BloomFilter.Configuration;
using ByTech.BloomFilter.Storage;
using FluentAssertions;
using Xunit;

namespace ByTech.BloomFilter.Tests.Storage;

public class BitStoreTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void New_store_has_all_bits_zero()
    {
        var store = new BitStore(128);

        for (long i = 0; i < 128; i++)
        {
            store.GetBit(i).Should().BeFalse($"bit {i} should be zero initially");
        }

        store.PopCount().Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SetBit_and_GetBit_round_trip()
    {
        var store = new BitStore(256);

        store.SetBit(0);
        store.SetBit(42);
        store.SetBit(63);
        store.SetBit(64);
        store.SetBit(127);
        store.SetBit(255);

        store.GetBit(0).Should().BeTrue();
        store.GetBit(42).Should().BeTrue();
        store.GetBit(63).Should().BeTrue();
        store.GetBit(64).Should().BeTrue();
        store.GetBit(127).Should().BeTrue();
        store.GetBit(255).Should().BeTrue();

        // Unset bits should still be false
        store.GetBit(1).Should().BeFalse();
        store.GetBit(41).Should().BeFalse();
        store.GetBit(128).Should().BeFalse();
    }

    [Theory]
    [InlineData(62)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [Trait("Category", "Unit")]
    public void Word_boundary_bits_are_correct(int bitIndex)
    {
        var store = new BitStore(128);

        store.SetBit(bitIndex);

        store.GetBit(bitIndex).Should().BeTrue();

        // Adjacent bits should not be affected
        if (bitIndex > 0)
        {
            store.GetBit(bitIndex - 1).Should().BeFalse();
        }

        if (bitIndex < 127)
        {
            store.GetBit(bitIndex + 1).Should().BeFalse();
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SetBit_is_idempotent()
    {
        var store = new BitStore(64);

        store.SetBit(10);
        store.SetBit(10);
        store.SetBit(10);

        store.GetBit(10).Should().BeTrue();
        store.PopCount().Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Clear_resets_all_bits()
    {
        var store = new BitStore(256);

        for (long i = 0; i < 256; i++)
        {
            store.SetBit(i);
        }

        store.PopCount().Should().Be(256);

        store.Clear();

        store.PopCount().Should().Be(0);

        for (long i = 0; i < 256; i++)
        {
            store.GetBit(i).Should().BeFalse();
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void PopCount_returns_correct_count()
    {
        var store = new BitStore(1024);

        store.SetBit(0);
        store.SetBit(100);
        store.SetBit(500);
        store.SetBit(999);
        store.SetBit(1023);

        store.PopCount().Should().Be(5);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void WordCount_is_ceiling_division_by_64()
    {
        new BitStore(1).WordCount.Should().Be(1);
        new BitStore(64).WordCount.Should().Be(1);
        new BitStore(65).WordCount.Should().Be(2);
        new BitStore(128).WordCount.Should().Be(2);
        new BitStore(129).WordCount.Should().Be(3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BitCount_matches_construction_parameter()
    {
        new BitStore(100).BitCount.Should().Be(100);
        new BitStore(9_585_059).BitCount.Should().Be(9_585_059);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Non_64_aligned_bit_count_works_correctly()
    {
        // 100 bits → 2 words (128 bits of storage, but only 100 logical bits)
        var store = new BitStore(100);
        store.WordCount.Should().Be(2);

        // Set the last valid bit
        store.SetBit(99);
        store.GetBit(99).Should().BeTrue();

        store.PopCount().Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    [Trait("Category", "Unit")]
    public void Rejects_non_positive_bit_count(long bitCount)
    {
        var act = () => new BitStore(bitCount);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetWords_returns_correct_data()
    {
        var store = new BitStore(128);
        store.SetBit(0);    // word 0, bit 0
        store.SetBit(64);   // word 1, bit 0

        var words = store.GetWords();
        words.Length.Should().Be(2);
        words[0].Should().Be(1UL);
        words[1].Should().Be(1UL);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Large_store_handles_millions_of_bits()
    {
        var store = new BitStore(10_000_000);

        store.SetBit(0);
        store.SetBit(4_999_999);
        store.SetBit(9_999_999);

        store.GetBit(0).Should().BeTrue();
        store.GetBit(4_999_999).Should().BeTrue();
        store.GetBit(9_999_999).Should().BeTrue();
        store.GetBit(5_000_000).Should().BeFalse();

        store.PopCount().Should().Be(3);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Parameter_calculator_output_creates_valid_store()
    {
        // Simulate parameter calculator output for 1M items at 1%
        var options = new BloomFilterOptions(1_000_000, 0.01);
        var parameters = BloomFilterCalculator.Compute(options);

        var store = new BitStore(parameters.BitCount);

        store.BitCount.Should().Be(parameters.BitCount);
        store.WordCount.Should().Be(parameters.WordCount);
        store.PopCount().Should().Be(0);

        // Set some bits and verify
        store.SetBit(0);
        store.SetBit(parameters.BitCount - 1);
        store.PopCount().Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void All_bits_in_single_word_can_be_set_independently()
    {
        var store = new BitStore(64);

        for (long i = 0; i < 64; i++)
        {
            store.SetBit(i);
        }

        store.PopCount().Should().Be(64);

        for (long i = 0; i < 64; i++)
        {
            store.GetBit(i).Should().BeTrue();
        }
    }
}
