using System.Buffers.Binary;
using ByTech.BloomFilter.Serialization;
using FluentAssertions;
using Xunit;

namespace ByTech.BloomFilter.Tests.Serialization;

public class BloomFilterSerializerTests
{
    private static BloomFilter CreatePopulatedFilter(int itemCount = 1000)
    {
        var filter = BloomFilterBuilder
            .ForExpectedInsertions(itemCount)
            .WithFalsePositiveRate(0.01)
            .Build();

        var key = new byte[4];
        for (var i = 0; i < itemCount; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(key, i);
            filter.Add(key);
        }

        return filter;
    }

    private static byte[] Serialize(BloomFilter filter)
    {
        using var ms = new MemoryStream();
        BloomFilterSerializer.WriteTo(filter, ms);
        return ms.ToArray();
    }

    private static BloomFilter Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        return BloomFilterSerializer.ReadFrom(ms);
    }

    // --- Round-trip tests ---

    [Fact]
    [Trait("Category", "Unit")]
    public void Round_trip_preserves_metadata()
    {
        var original = CreatePopulatedFilter();
        var data = Serialize(original);
        var restored = Deserialize(data);

        restored.ExpectedInsertions.Should().Be(original.ExpectedInsertions);
        restored.TargetFalsePositiveRate.Should().Be(original.TargetFalsePositiveRate);
        restored.BitCount.Should().Be(original.BitCount);
        restored.HashFunctionCount.Should().Be(original.HashFunctionCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Round_trip_preserves_membership()
    {
        var original = CreatePopulatedFilter(500);
        var data = Serialize(original);
        var restored = Deserialize(data);

        var key = new byte[4];
        for (var i = 0; i < 500; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(key, i);
            restored.MayContain(key).Should().BeTrue($"item {i} should be present after round-trip");
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Multiple_round_trips_produce_identical_output()
    {
        var original = CreatePopulatedFilter(100);
        var data1 = Serialize(original);
        var restored1 = Deserialize(data1);
        var data2 = Serialize(restored1);

        data1.Should().Equal(data2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Empty_filter_round_trips()
    {
        var filter = BloomFilterBuilder
            .ForExpectedInsertions(1000)
            .WithFalsePositiveRate(0.01)
            .Build();

        var data = Serialize(filter);
        var restored = Deserialize(data);

        restored.Snapshot().BitsSet.Should().Be(0);
        restored.BitCount.Should().Be(filter.BitCount);
    }

    // --- Corruption and rejection tests ---

    [Fact]
    [Trait("Category", "Unit")]
    public void Rejects_empty_stream()
    {
        var act = () => Deserialize([]);
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Rejects_truncated_header()
    {
        var data = Serialize(CreatePopulatedFilter());
        var truncated = data[..20]; // Header is 40 bytes

        var act = () => Deserialize(truncated);
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Rejects_wrong_magic_bytes()
    {
        var data = Serialize(CreatePopulatedFilter());
        data[0] = 0xFF; // Corrupt magic

        var act = () => Deserialize(data);
        act.Should().Throw<InvalidDataException>().WithMessage("*magic*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Rejects_wrong_version()
    {
        var data = Serialize(CreatePopulatedFilter());
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 99); // Bad version

        var act = () => Deserialize(data);
        act.Should().Throw<InvalidDataException>().WithMessage("*version*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Rejects_checksum_mismatch()
    {
        var data = Serialize(CreatePopulatedFilter());
        // Corrupt a byte in the word data (after header, before checksum)
        data[42] ^= 0xFF;

        var act = () => Deserialize(data);
        act.Should().Throw<InvalidDataException>().WithMessage("*checksum*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Rejects_truncated_word_data()
    {
        var data = Serialize(CreatePopulatedFilter());
        // Truncate to header + partial word data (no checksum)
        var truncated = data[..50];

        var act = () => Deserialize(truncated);
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Rejects_random_garbage()
    {
        var random = new Random(42);
        var garbage = new byte[100];
        random.NextBytes(garbage);

        var act = () => Deserialize(garbage);
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Rejects_excessive_bit_count_in_header()
    {
        var data = Serialize(CreatePopulatedFilter(10));
        // Overwrite bitCount with a huge value (offset 24, 8 bytes LE)
        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(24), long.MaxValue);

        var act = () => Deserialize(data);
        act.Should().Throw<InvalidDataException>().WithMessage("*bit count*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Rejects_excessive_hash_function_count_in_header()
    {
        var data = Serialize(CreatePopulatedFilter(10));
        // Overwrite hashFunctionCount with a huge value (offset 32, 4 bytes LE)
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(32), int.MaxValue);

        var act = () => Deserialize(data);
        act.Should().Throw<InvalidDataException>().WithMessage("*hash function*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Rejects_zero_expected_insertions_in_header()
    {
        var data = Serialize(CreatePopulatedFilter(10));
        // Overwrite expectedInsertions with 0 (offset 8, 8 bytes LE)
        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(8), 0);

        var act = () => Deserialize(data);
        act.Should().Throw<InvalidDataException>();
    }

    // --- Full pipeline integration tests ---

    [Fact]
    [Trait("Category", "Integration")]
    public void Full_pipeline_add_serialize_deserialize_query_10K()
    {
        const int n = 10_000;
        var filter = BloomFilterBuilder
            .ForExpectedInsertions(n)
            .WithFalsePositiveRate(0.01)
            .Build();

        var key = new byte[4];
        for (var i = 0; i < n; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(key, i);
            filter.Add(key);
        }

        // Serialize and deserialize
        var data = Serialize(filter);
        var restored = Deserialize(data);

        // Verify zero false negatives
        for (var i = 0; i < n; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(key, i);
            restored.MayContain(key).Should().BeTrue($"item {i} was lost after serialize/deserialize");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Deserialize_then_add_more_items()
    {
        var filter = CreatePopulatedFilter(500);
        var data = Serialize(filter);
        var restored = Deserialize(data);

        // Add more items to the restored filter
        var key = new byte[4];
        for (var i = 500; i < 1000; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(key, i);
            restored.Add(key);
        }

        // All 1000 items should be found
        for (var i = 0; i < 1000; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(key, i);
            restored.MayContain(key).Should().BeTrue($"item {i} not found");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Tampered_payload_is_detected()
    {
        var filter = CreatePopulatedFilter(100);
        var data = Serialize(filter);

        // Tamper with word data, but update nothing else
        data[44] ^= 0x01;

        var act = () => Deserialize(data);
        act.Should().Throw<InvalidDataException>().WithMessage("*checksum*");
    }
}
