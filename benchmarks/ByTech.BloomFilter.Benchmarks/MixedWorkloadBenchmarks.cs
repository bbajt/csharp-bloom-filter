using BenchmarkDotNet.Attributes;

namespace ByTech.BloomFilter.Benchmarks;

/// <summary>
/// Measures mixed add/query workloads approximating realistic usage.
/// </summary>
[MemoryDiagnoser]
public class MixedWorkloadBenchmarks
{
    private BloomFilter _filter = null!;
    private byte[][] _keys = null!;
    private int _index;

    [Params(100_000)]
    public int ExpectedInsertions { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _filter = BloomFilterBuilder
            .ForExpectedInsertions(ExpectedInsertions)
            .WithFalsePositiveRate(0.01)
            .Build();

        var rng = new Random(42);
        _keys = new byte[2048][];
        for (var i = 0; i < 2048; i++)
        {
            _keys[i] = new byte[16];
            rng.NextBytes(_keys[i]);
        }

        // Pre-populate half
        for (var i = 0; i < 1024; i++)
        {
            _filter.Add(_keys[i]);
        }

        _index = 0;
    }

    [Benchmark(Description = "Add+Query alternating")]
    public bool AddThenQuery()
    {
        var idx = _index++ & 2047;
        if ((idx & 1) == 0)
        {
            _filter.Add(_keys[idx]);
            return true;
        }

        return _filter.MayContain(_keys[idx]);
    }
}
