# ByTech.BloomFilter

[![NuGet](https://img.shields.io/nuget/v/ByTech.BloomFilter.svg?label=nuget&color=blue)](https://www.nuget.org/packages/ByTech.BloomFilter)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ByTech.BloomFilter.svg?color=blue)](https://www.nuget.org/packages/ByTech.BloomFilter)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](#)
[![Tests](https://img.shields.io/badge/tests-213-brightgreen)](#)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](LICENSE.MD)

A high-performance, benchmark-driven **Bloom filter** library for .NET 10. Zero-allocation hot path, span-first API, versioned binary serialization, thread-safe variant, counting filter with deletion, batch operations, named filter factory, DI integration, and strong statistical validation.

---

## What is a Bloom filter?

A Bloom filter is a space-efficient probabilistic data structure for membership testing. It can tell you **"definitely not in the set"** or **"possibly in the set"** — never the reverse.

- **No false negatives** — if an element was added, `MayContain` always returns `true`
- **Possible false positives** — `MayContain` may return `true` for elements never added
- **Space-efficient** — uses far less memory than a hash set for large cardinalities
- **O(k) operations** — both `Add` and `MayContain` are constant-time

Common use cases: cache filtering, duplicate detection, network routing, database query optimization, and pre-screening large datasets.

---

## Quick start

```csharp
using ByTech.BloomFilter;

// Standard filter
var filter = BloomFilterBuilder
    .ForExpectedInsertions(1_000_000)
    .WithFalsePositiveRate(0.01)
    .Build();

filter.Add("hello"u8);
filter.Add("world");
filter.MayContain("hello"u8);  // true
filter.MayContain("other");    // false

// Thread-safe filter (lock-free adds)
using var safeFilter = BloomFilterBuilder
    .ForExpectedInsertions(1_000_000)
    .WithFalsePositiveRate(0.01)
    .BuildThreadSafe();

// Counting filter (supports deletion)
var counting = CountingBloomFilterBuilder
    .ForExpectedInsertions(100_000)
    .WithFalsePositiveRate(0.01)
    .Build();

counting.Add("item"u8);
counting.MayContain("item"u8);  // true
counting.Remove("item"u8);      // true (decremented)
counting.MayContain("item"u8);  // false

// Batch operations (works on any IBloomFilter)
IBloomFilter bf = filter;
bf.AddRange(new[] { "a"u8.ToArray(), "b"u8.ToArray() }
    .Select(x => (ReadOnlyMemory<byte>)x).ToArray());
bf.ContainsAll(...);  // all present?
bf.ContainsAny(...);  // any present?

// DI integration (separate package: ByTech.BloomFilter.DependencyInjection)
services.AddBloomFilter(bf =>
{
    bf.AddFilter("users", b => b.WithExpectedInsertions(1_000_000).WithFalsePositiveRate(0.01));
    bf.AddThreadSafeFilter("sessions", b => b.WithExpectedInsertions(500_000).WithFalsePositiveRate(0.001));
});

// Resolve via factory
var factory = serviceProvider.GetRequiredService<IBloomFilterFactory>();
var usersFilter = factory.Get("users");
```

---

## Features

- **Zero-allocation hot path** — `Add` and `MayContain` allocate nothing on the heap
- **Span-first API** — primary path is `ReadOnlySpan<byte>`; string and byte[] overloads delegate to it
- **Thread-safe variant** — `ThreadSafeBloomFilter` with lock-free `Interlocked.Or` for concurrent adds
- **Counting Bloom filter** — `CountingBloomFilter` with 4-bit counters supporting Add/Remove/MayContain
- **IBloomFilter interface** — common surface for all filter types; polymorphic usage via factory or DI
- **Batch operations** — `AddRange`, `ContainsAll`, `ContainsAny` on all filter types + string/generic extensions
- **Named filter factory** — `IBloomFilterFactory` / `BloomFilterFactory` for managing multiple named filters
- **DI integration** — `ByTech.BloomFilter.DependencyInjection` package with `AddBloomFilter()` for `IServiceCollection`
- **Generic T support** — `IBloomFilterKeySerializer<T>` + extension methods for typed keys
- **Double-hashing position derivation** — XxHash128 split into h1/h2, generating `k` positions in O(1)
- **Packed `ulong[]` bit storage** — 64-bit word operations for high throughput
- **Configurable parameters** — specify expected insertions and target false positive rate; optimal `m` and `k` computed automatically
- **Versioned binary serialization** — persist and restore with round-trip correctness and CRC32 corruption detection
- **ETW telemetry** — opt-in `EventSource` counters (`bloom.items_added`, `bloom.queries`) with zero cost when no listener
- **Diagnostics** — saturation metrics, popcount, estimated current false positive rate

---

## API

### Standard Bloom filter

```csharp
var filter = BloomFilterBuilder
    .ForExpectedInsertions(10_000_000)
    .WithFalsePositiveRate(0.001)
    .Build();

filter.Add("key"u8);
filter.MayContain("key"u8);  // true
filter.Clear();
filter.Snapshot();            // diagnostics: bits set, fill ratio, estimated FPR
```

### Thread-safe Bloom filter

```csharp
using var filter = BloomFilterBuilder
    .ForExpectedInsertions(1_000_000)
    .WithFalsePositiveRate(0.01)
    .BuildThreadSafe();

// Safe for concurrent Add + MayContain from multiple threads
// Add uses lock-free Interlocked.Or — no contention between writers
// Clear acquires exclusive lock
```

### Counting Bloom filter

```csharp
var filter = CountingBloomFilterBuilder
    .ForExpectedInsertions(100_000)
    .WithFalsePositiveRate(0.01)
    .Build();

filter.Add("item"u8);
filter.MayContain("item"u8);  // true
filter.Remove("item"u8);      // true — counters decremented
filter.MayContain("item"u8);  // false

// 4-bit counters, saturate at 15 (sticky — prevents overflow-induced false negatives)
// Uses 4x memory of standard filter
```

### Batch operations

```csharp
// Available on all filter types via IBloomFilter
IBloomFilter filter = BloomFilterBuilder
    .ForExpectedInsertions(100_000)
    .WithFalsePositiveRate(0.01)
    .Build();

// Byte-based batch
var items = new ReadOnlyMemory<byte>[] { "a"u8.ToArray(), "b"u8.ToArray(), "c"u8.ToArray() };
filter.AddRange(items);
filter.ContainsAll(items);  // true — all present
filter.ContainsAny(items);  // true — at least one present

// String batch (extension methods)
filter.AddRange(new[] { "hello", "world" });
filter.ContainsAll(new[] { "hello", "world" });  // true
```

### Named filter factory

```csharp
var factory = new BloomFilterFactory();
factory.Register("users", BloomFilterBuilder.ForExpectedInsertions(1_000_000).WithFalsePositiveRate(0.01).Build());
factory.Register("sessions", BloomFilterBuilder.ForExpectedInsertions(500_000).WithFalsePositiveRate(0.001).BuildThreadSafe());

var usersFilter = factory.Get("users");
factory.TryGet("missing", out var f);  // false
```

### DI integration

```csharp
// Package: ByTech.BloomFilter.DependencyInjection
services.AddBloomFilter(bf =>
{
    bf.AddFilter("users", b => b.WithExpectedInsertions(1_000_000).WithFalsePositiveRate(0.01));
    bf.AddThreadSafeFilter("sessions", b => b.WithExpectedInsertions(500_000).WithFalsePositiveRate(0.001));
    bf.AddCountingFilter("temp-keys", b => b.WithExpectedInsertions(10_000).WithFalsePositiveRate(0.05));
});

// Resolve anywhere via IBloomFilterFactory
var factory = serviceProvider.GetRequiredService<IBloomFilterFactory>();
var filter = factory.Get("users");
```

### Generic T via serializer

```csharp
public class GuidSerializer : IBloomFilterKeySerializer<Guid>
{
    public int GetMaxByteCount(Guid value) => 16;
    public int Serialize(Guid value, Span<byte> destination)
    {
        value.TryWriteBytes(destination);
        return 16;
    }
}

var filter = BloomFilterBuilder.ForExpectedInsertions(10_000).WithFalsePositiveRate(0.01).Build();
var serializer = new GuidSerializer();

filter.Add(myGuid, serializer);
filter.MayContain(myGuid, serializer);
```

### Serialization

```csharp
// Save
using var stream = File.Create("filter.bin");
BloomFilterSerializer.WriteTo(filter, stream);

// Load
using var input = File.OpenRead("filter.bin");
var restored = BloomFilterSerializer.ReadFrom(input);
```

---

## Benchmarks

Environment: .NET 10.0.5, x64 RyuJIT, Release, Windows 11

### Add throughput

| Method | Key size | Mean | Allocated |
|---|---|---|---|
| Add(byte[]) | 16 B | 32 ns | 0 B |
| Add(byte[]) | 128 B | 42 ns | 0 B |
| Add(string) | 16 B | 55 ns | 0 B |
| Add(string) | 128 B | 68 ns | 0 B |

### Query throughput

| Method | Mean | Allocated |
|---|---|---|
| MayContain(present) | 32 ns | 0 B |
| MayContain(absent) | 22 ns | 0 B |

**Zero heap allocations** confirmed across all hot-path operations via `[MemoryDiagnoser]`.

```bash
dotnet run --project benchmarks/ByTech.BloomFilter.Benchmarks -c Release
```

See [benchmark methodology](.docs/kb/benchmark-methodology.md) for environment requirements and interpretation rules.

---

## How it works

### Parameter calculation

Given expected insertions `n` and target false positive rate `p`:

```
m = -(n × ln(p)) / (ln(2)²)     — optimal bit count
k = (m / n) × ln(2)              — optimal hash function count
```

### Double hashing

XxHash128 produces 128 bits → split into h1 (low 64) and h2 (high 64). Positions derived via:

```
position(i) = (h1 + i × h2) mod m    for i = 0, 1, ..., k-1
```

### Bit storage

Bits are packed into a `ulong[]` array. Set and test operations use word-level indexing and bit masking — no per-operation allocations. The thread-safe variant uses `Interlocked.Or` for atomic bit-setting.

---

## Thread safety

| Type | Concurrent Add | Concurrent Query | Concurrent Add+Query | Clear |
|---|---|---|---|---|
| `BloomFilter` | No | No | No | No |
| `ThreadSafeBloomFilter` | Yes (lock-free) | Yes | Yes | Exclusive lock |

The standard `BloomFilter` requires external synchronization. `ThreadSafeBloomFilter` uses `Interlocked.Or` for lock-free atomic bit-setting — bits only transition 0→1, making this safe without locks.

---

## Telemetry

Opt-in ETW counters via `System.Diagnostics.Tracing.EventSource`:

| Counter | Type | Description |
|---|---|---|
| `bloom.items_added` | Rate | Items added per second |
| `bloom.queries` | Rate | Queries per second |
| `bloom.false_positive_estimate` | Gauge | Estimated current FPR |

```bash
dotnet counters monitor --process-id <pid> --counters ByTech.BloomFilter
```

Zero overhead when no listener is attached.

---

## Project structure

```
ByTech.BloomFilter.slnx
├── src/
│   ├── ByTech.BloomFilter/                          # Core library
│   │   ├── Configuration/                           # Parameter planning, validation
│   │   ├── Hashing/                                 # XxHash128 double-hashing
│   │   ├── Storage/                                 # BitStore, ConcurrentBitStore, CountingBitStore
│   │   ├── Serialization/                           # Versioned binary format with CRC32
│   │   └── Diagnostics/                             # Snapshot, EventSource
│   └── ByTech.BloomFilter.DependencyInjection/      # DI integration package
├── tests/
│   ├── ByTech.BloomFilter.Tests/                    # Unit, integration, statistical, DI
│   └── ByTech.BloomFilter.FuzzTests/                # Fuzz testing
├── benchmarks/
│   └── ByTech.BloomFilter.Benchmarks/               # BenchmarkDotNet suite (5 classes)
└── .docs/                                           # Project documentation
```

---

## Limitations

- **Counting filter uses 4x memory** — 4-bit counters vs 1-bit positions
- **Counter saturation** — counters at 15 are sticky and cannot be decremented
- **Little-endian serialization** — binary format assumes little-endian host (standard for all .NET 10 platforms)
- **Maximum capacity** — 2^37 bits (~16 GB); exceeding this throws during construction
- **Counting filter serialization** — not yet implemented (standard filter serialization only)

---

## Version

Current: **v1.2.0**

---

## License

Copyright 2026 ByTech. Licensed under the [Apache License, Version 2.0](LICENSE.MD).
