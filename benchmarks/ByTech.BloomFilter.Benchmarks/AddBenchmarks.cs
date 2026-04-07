using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;

namespace ByTech.BloomFilter.Benchmarks;

/// <summary>
/// Measures Add throughput for various input sizes and filter configurations.
/// </summary>
[MemoryDiagnoser]
public class AddBenchmarks
{
    private BloomFilter _filter = null!;
    private byte[][] _keys = null!;
    private string[] _stringKeys = null!;
    private int _index;

    [Params(10_000, 100_000, 1_000_000)]
    public int ExpectedInsertions { get; set; }

    [Params(16, 128)]
    public int KeySizeBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _filter = BloomFilterBuilder
            .ForExpectedInsertions(ExpectedInsertions)
            .WithFalsePositiveRate(0.01)
            .Build();

        // Pre-generate keys to avoid measuring key generation
        var rng = new Random(42);
        _keys = new byte[1024][];
        _stringKeys = new string[1024];
        for (var i = 0; i < 1024; i++)
        {
            _keys[i] = new byte[KeySizeBytes];
            rng.NextBytes(_keys[i]);
            _stringKeys[i] = $"key-{i:D6}-{new string('x', Math.Max(0, KeySizeBytes - 16))}";
        }

        _index = 0;
    }

    [Benchmark(Description = "Add(byte[])")]
    public void AddBytes()
    {
        _filter.Add(_keys[_index++ & 1023]);
    }

    [Benchmark(Description = "Add(string)")]
    public void AddString()
    {
        _filter.Add(_stringKeys[_index++ & 1023]);
    }
}
