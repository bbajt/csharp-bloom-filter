using System.Buffers.Binary;
using FluentAssertions;
using Xunit;

namespace ByTech.BloomFilter.Tests;

public class BloomFilterExtensionsTests
{
    // --- Test serializers ---

    private sealed class Int32Serializer : IBloomFilterKeySerializer<int>
    {
        public int GetMaxByteCount(int value) => 4;

        public int Serialize(int value, Span<byte> destination)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, value);
            return 4;
        }
    }

    private sealed class GuidSerializer : IBloomFilterKeySerializer<Guid>
    {
        public int GetMaxByteCount(Guid value) => 16;

        public int Serialize(Guid value, Span<byte> destination)
        {
            value.TryWriteBytes(destination);
            return 16;
        }
    }

    private record UserRecord(int Id, string Name);

    private sealed class UserRecordSerializer : IBloomFilterKeySerializer<UserRecord>
    {
        public int GetMaxByteCount(UserRecord value) => 4 + System.Text.Encoding.UTF8.GetMaxByteCount(value.Name.Length);

        public int Serialize(UserRecord value, Span<byte> destination)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, value.Id);
            var nameBytes = System.Text.Encoding.UTF8.GetBytes(value.Name.AsSpan(), destination[4..]);
            return 4 + nameBytes;
        }
    }

    // --- Tests ---

    [Fact]
    [Trait("Category", "Unit")]
    public void Add_and_query_int_via_serializer()
    {
        var filter = BloomFilterBuilder.ForExpectedInsertions(1000).WithFalsePositiveRate(0.01).Build();
        var serializer = new Int32Serializer();

        filter.Add(42, serializer);
        filter.Add(100, serializer);

        filter.MayContain(42, serializer).Should().BeTrue();
        filter.MayContain(100, serializer).Should().BeTrue();
        filter.MayContain(999, serializer).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Add_and_query_guid_via_serializer()
    {
        var filter = BloomFilterBuilder.ForExpectedInsertions(1000).WithFalsePositiveRate(0.01).Build();
        var serializer = new GuidSerializer();

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        filter.Add(id1, serializer);
        filter.Add(id2, serializer);

        filter.MayContain(id1, serializer).Should().BeTrue();
        filter.MayContain(id2, serializer).Should().BeTrue();
        filter.MayContain(id3, serializer).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Add_and_query_custom_record_via_serializer()
    {
        var filter = BloomFilterBuilder.ForExpectedInsertions(1000).WithFalsePositiveRate(0.01).Build();
        var serializer = new UserRecordSerializer();

        var user1 = new UserRecord(1, "Alice");
        var user2 = new UserRecord(2, "Bob");
        var user3 = new UserRecord(3, "Charlie");

        filter.Add(user1, serializer);
        filter.Add(user2, serializer);

        filter.MayContain(user1, serializer).Should().BeTrue();
        filter.MayContain(user2, serializer).Should().BeTrue();
        filter.MayContain(user3, serializer).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Thread_safe_filter_extension_methods_work()
    {
        using var filter = BloomFilterBuilder.ForExpectedInsertions(1000).WithFalsePositiveRate(0.01).BuildThreadSafe();
        var serializer = new Int32Serializer();

        filter.Add(42, serializer);
        filter.MayContain(42, serializer).Should().BeTrue();
        filter.MayContain(99, serializer).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Bulk_int_add_query_via_serializer()
    {
        var filter = BloomFilterBuilder.ForExpectedInsertions(10_000).WithFalsePositiveRate(0.01).Build();
        var serializer = new Int32Serializer();

        for (var i = 0; i < 10_000; i++)
        {
            filter.Add(i, serializer);
        }

        for (var i = 0; i < 10_000; i++)
        {
            filter.MayContain(i, serializer).Should().BeTrue($"item {i} not found");
        }
    }

    // --- Large-value serializer for ArrayPool path ---

    private sealed class LargeValueSerializer : IBloomFilterKeySerializer<int>
    {
        public int GetMaxByteCount(int value) => 600; // exceeds 512 threshold
        public int Serialize(int value, Span<byte> destination)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, value);
            destination[4..600].Clear();
            return 600;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Large_value_serializer_uses_array_pool_path()
    {
        var filter = BloomFilterBuilder.ForExpectedInsertions(1000).WithFalsePositiveRate(0.01).Build();
        var serializer = new LargeValueSerializer();

        filter.Add(42, serializer);
        filter.MayContain(42, serializer).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Large_value_serializer_with_thread_safe_filter()
    {
        using var filter = BloomFilterBuilder.ForExpectedInsertions(1000).WithFalsePositiveRate(0.01).BuildThreadSafe();
        var serializer = new LargeValueSerializer();

        filter.Add(42, serializer);
        filter.MayContain(42, serializer).Should().BeTrue();
    }
}
