using BenchmarkDotNet.Attributes;
using ByTech.BloomFilter.Serialization;

namespace ByTech.BloomFilter.Benchmarks;

/// <summary>
/// Measures serialization and deserialization throughput.
/// </summary>
[MemoryDiagnoser]
public class SerializationBenchmarks
{
    private BloomFilter _filter = null!;
    private byte[] _serialized = null!;
    private MemoryStream _writeStream = null!;
    private MemoryStream _readStream = null!;

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
        var key = new byte[16];
        var count = Math.Min(ExpectedInsertions, 100_000); // cap to keep setup fast
        for (var i = 0; i < count; i++)
        {
            rng.NextBytes(key);
            _filter.Add(key);
        }

        using var ms = new MemoryStream();
        BloomFilterSerializer.WriteTo(_filter, ms);
        _serialized = ms.ToArray();

        _writeStream = new MemoryStream(_serialized.Length);
        _readStream = new MemoryStream(_serialized);
    }

    [Benchmark(Description = "WriteTo")]
    public void Write()
    {
        _writeStream.Position = 0;
        BloomFilterSerializer.WriteTo(_filter, _writeStream);
    }

    [Benchmark(Description = "ReadFrom")]
    public BloomFilter Read()
    {
        _readStream.Position = 0;
        return BloomFilterSerializer.ReadFrom(_readStream);
    }
}
