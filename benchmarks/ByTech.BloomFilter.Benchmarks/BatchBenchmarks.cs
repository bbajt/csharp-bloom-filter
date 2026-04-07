using BenchmarkDotNet.Attributes;

namespace ByTech.BloomFilter.Benchmarks;

/// <summary>
/// Measures batch operation throughput vs single-item loops.
/// </summary>
[MemoryDiagnoser]
public class BatchBenchmarks
{
    private BloomFilter _filter = null!;
    private ReadOnlyMemory<byte>[] _batchItems = null!;
    private string[] _stringItems = null!;

    [Params(10, 100, 1000)]
    public int BatchSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _filter = BloomFilterBuilder
            .ForExpectedInsertions(100_000)
            .WithFalsePositiveRate(0.01)
            .Build();

        var rng = new Random(42);
        _batchItems = new ReadOnlyMemory<byte>[BatchSize];
        _stringItems = new string[BatchSize];

        for (var i = 0; i < BatchSize; i++)
        {
            var key = new byte[16];
            rng.NextBytes(key);
            _batchItems[i] = key;
            _stringItems[i] = $"key-{i:D6}";
        }

        // Pre-populate half the items so ContainsAll/ContainsAny have mixed results
        for (var i = 0; i < BatchSize / 2; i++)
        {
            _filter.Add(_batchItems[i].Span);
        }
    }

    [Benchmark(Description = "AddRange(Memory<byte>[])")]
    public void AddRangeBatch()
    {
        _filter.AddRange(_batchItems);
    }

    [Benchmark(Description = "Add loop (byte[])")]
    public void AddLoop()
    {
        foreach (var item in _batchItems)
        {
            _filter.Add(item.Span);
        }
    }

    [Benchmark(Description = "ContainsAll(Memory<byte>[])")]
    public bool ContainsAllBatch()
    {
        return _filter.ContainsAll(_batchItems);
    }

    [Benchmark(Description = "ContainsAny(Memory<byte>[])")]
    public bool ContainsAnyBatch()
    {
        return _filter.ContainsAny(_batchItems);
    }

    [Benchmark(Description = "AddRange(string[])")]
    public void AddRangeStrings()
    {
        _filter.AddRange(_stringItems);
    }

    [Benchmark(Description = "ContainsAll(string[])")]
    public bool ContainsAllStrings()
    {
        return _filter.ContainsAll(_stringItems);
    }
}
