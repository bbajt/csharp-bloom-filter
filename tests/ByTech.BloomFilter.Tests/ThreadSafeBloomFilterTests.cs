using System.Buffers.Binary;
using FluentAssertions;
using Xunit;

namespace ByTech.BloomFilter.Tests;

public class ThreadSafeBloomFilterTests
{
    private static ThreadSafeBloomFilter CreateFilter(long n = 10_000, double p = 0.01)
    {
        return BloomFilterBuilder
            .ForExpectedInsertions(n)
            .WithFalsePositiveRate(p)
            .BuildThreadSafe();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Added_item_is_found()
    {
        using var filter = CreateFilter();
        filter.Add("hello"u8);
        filter.MayContain("hello"u8).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Non_added_item_is_not_found()
    {
        using var filter = CreateFilter();
        filter.Add("hello"u8);
        filter.MayContain("world"u8).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void String_overloads_work()
    {
        using var filter = CreateFilter();
        filter.Add("test-string");
        filter.MayContain("test-string").Should().BeTrue();
        filter.MayContain("other").Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Clear_resets_filter()
    {
        using var filter = CreateFilter();
        filter.Add("hello"u8);
        filter.Clear();
        filter.MayContain("hello"u8).Should().BeFalse();
        filter.Snapshot().BitsSet.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Snapshot_returns_metrics()
    {
        using var filter = CreateFilter(n: 1000, p: 0.01);
        filter.Add("item1"u8);
        filter.Add("item2"u8);

        var snap = filter.Snapshot();
        snap.BitsSet.Should().BeGreaterThan(0);
        snap.BitCount.Should().Be(filter.BitCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Null_string_throws()
    {
        using var filter = CreateFilter();
        var addAct = () => filter.Add((string)null!);
        var queryAct = () => filter.MayContain((string)null!);
        addAct.Should().Throw<ArgumentNullException>();
        queryAct.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Concurrent_adds_produce_zero_false_negatives()
    {
        const int itemsPerThread = 5_000;
        const int threadCount = 4;
        const int totalItems = itemsPerThread * threadCount;

        using var filter = CreateFilter(n: totalItems, p: 0.01);

        // Each thread adds a distinct range of keys
        var tasks = new Task[threadCount];
        for (var t = 0; t < threadCount; t++)
        {
            var threadId = t;
            tasks[t] = Task.Run(() =>
            {
                var key = new byte[8];
                var start = threadId * itemsPerThread;
                for (var i = start; i < start + itemsPerThread; i++)
                {
                    BinaryPrimitives.WriteInt64LittleEndian(key, i);
                    filter.Add(key);
                }
            });
        }

        await Task.WhenAll(tasks);

        // Verify zero false negatives
        var key2 = new byte[8];
        for (var i = 0; i < totalItems; i++)
        {
            BinaryPrimitives.WriteInt64LittleEndian(key2, i);
            filter.MayContain(key2).Should().BeTrue($"item {i} was added concurrently but not found");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Concurrent_queries_do_not_crash()
    {
        using var filter = CreateFilter(n: 10_000, p: 0.01);

        // Pre-populate
        var key = new byte[4];
        for (var i = 0; i < 1000; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(key, i);
            filter.Add(key);
        }

        // Concurrent queries
        var tasks = new Task[8];
        for (var t = 0; t < 8; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                var k = new byte[4];
                for (var i = 0; i < 5000; i++)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(k, i);
                    _ = filter.MayContain(k);
                }
            });
        }

        await Task.WhenAll(tasks);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Mixed_concurrent_add_and_query()
    {
        const int items = 10_000;
        using var filter = CreateFilter(n: items, p: 0.01);

        // Writers and readers running concurrently
        var writerTask = Task.Run(() =>
        {
            var key = new byte[8];
            for (var i = 0; i < items; i++)
            {
                BinaryPrimitives.WriteInt64LittleEndian(key, i);
                filter.Add(key);
            }
        });

        var readerTask = Task.Run(() =>
        {
            var key = new byte[8];
            for (var i = 0; i < items; i++)
            {
                BinaryPrimitives.WriteInt64LittleEndian(key, i);
                _ = filter.MayContain(key); // Result doesn't matter — just no crashes
            }
        });

        await Task.WhenAll(writerTask, readerTask);

        // After all writes complete, verify no false negatives
        var verifyKey = new byte[8];
        for (var i = 0; i < items; i++)
        {
            BinaryPrimitives.WriteInt64LittleEndian(verifyKey, i);
            filter.MayContain(verifyKey).Should().BeTrue($"item {i} not found after concurrent add");
        }
    }
}
