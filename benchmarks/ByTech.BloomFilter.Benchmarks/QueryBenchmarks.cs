using BenchmarkDotNet.Attributes;

namespace ByTech.BloomFilter.Benchmarks;

/// <summary>
/// Measures MayContain throughput for present and absent keys.
/// </summary>
[MemoryDiagnoser]
public class QueryBenchmarks
{
    private BloomFilter _filter = null!;
    private byte[][] _presentKeys = null!;
    private byte[][] _absentKeys = null!;
    private int _index;

    [Params(10_000, 1_000_000)]
    public int ExpectedInsertions { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _filter = BloomFilterBuilder
            .ForExpectedInsertions(ExpectedInsertions)
            .WithFalsePositiveRate(0.01)
            .Build();

        var rng = new Random(42);
        _presentKeys = new byte[1024][];
        _absentKeys = new byte[1024][];

        for (var i = 0; i < 1024; i++)
        {
            _presentKeys[i] = new byte[16];
            rng.NextBytes(_presentKeys[i]);
            _filter.Add(_presentKeys[i]);

            _absentKeys[i] = new byte[16];
            rng.NextBytes(_absentKeys[i]);
        }

        _index = 0;
    }

    [Benchmark(Description = "MayContain(present)")]
    public bool QueryPresent()
    {
        return _filter.MayContain(_presentKeys[_index++ & 1023]);
    }

    [Benchmark(Description = "MayContain(absent)")]
    public bool QueryAbsent()
    {
        return _filter.MayContain(_absentKeys[_index++ & 1023]);
    }
}
