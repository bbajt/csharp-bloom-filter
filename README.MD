# ByTech.BloomFilter

[![NuGet](https://img.shields.io/nuget/v/ByTech.BloomFilter.svg?label=nuget&color=blue)](https://www.nuget.org/packages/ByTech.BloomFilter)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ByTech.BloomFilter.svg?color=blue)](https://www.nuget.org/packages/ByTech.BloomFilter)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](#)
[![Tests](https://img.shields.io/badge/tests-109-brightgreen)](#)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](LICENSE.MD)

A high-performance, benchmark-driven **Bloom filter** library for .NET 10. Zero-allocation hot path, span-first API, versioned binary serialization, and strong statistical validation.

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

// Create a filter for 1 million items with 1% false positive rate
var filter = BloomFilterBuilder
    .ForExpectedInsertions(1_000_000)
    .WithFalsePositiveRate(0.01)
    .Build();

// Add items
filter.Add("hello"u8);
filter.Add("world");

// Query membership
filter.MayContain("hello"u8);  // true  — was added
filter.MayContain("world");    // true  — was added
filter.MayContain("other");    // false — probably not added (or rare false positive)
```

---

## Features

- **Zero-allocation hot path** — `Add` and `MayContain` allocate nothing on the heap
- **Span-first API** — primary path is `ReadOnlySpan<byte>`; string and byte[] overloads delegate to it
- **Double-hashing position derivation** — generates `k` bit positions from two hash values, avoiding `k` independent hash computations
- **Packed `ulong[]` bit storage** — 64-bit word operations for high throughput
- **Configurable parameters** — specify expected insertions and target false positive rate; the library computes optimal `m` and `k`
- **Versioned binary serialization** — persist and restore filters with round-trip correctness and corruption detection
- **Diagnostics** — saturation metrics, popcount, estimated current false positive rate

---

## API

### Core operations

```csharp
public sealed class BloomFilter
{
    // Configuration
    public long ExpectedInsertions { get; }
    public double TargetFalsePositiveRate { get; }
    public long BitCount { get; }
    public int HashFunctionCount { get; }

    // Membership
    public void Add(ReadOnlySpan<byte> value);
    public bool MayContain(ReadOnlySpan<byte> value);

    // String overloads (UTF-8 encoded, delegates to span path)
    public void Add(string value);
    public bool MayContain(string value);

    // Lifecycle
    public void Clear();
    public BloomFilterSnapshot Snapshot();
}
```

### Builder

```csharp
var filter = BloomFilterBuilder
    .ForExpectedInsertions(10_000_000)
    .WithFalsePositiveRate(0.001)
    .Build();
```

### Serialization

```csharp
// Save
using var stream = File.Create("filter.bin");
BloomFilterSerializer.WriteTo(filter, stream);

// Load
using var input = File.OpenRead("filter.bin");
var restored = BloomFilterSerializer.ReadFrom(input);

// Membership is preserved
restored.MayContain("hello"u8);  // same result as original
```

---

## How it works

### Parameter calculation

Given expected insertions `n` and target false positive rate `p`:

```
m = -(n × ln(p)) / (ln(2)²)     — optimal bit count
k = (m / n) × ln(2)              — optimal hash function count
```

### Double hashing

Instead of computing `k` independent hash functions, the library derives two 64-bit hashes (`h1`, `h2`) and generates positions:

```
position(i) = (h1 + i × h2) mod m    for i = 0, 1, ..., k-1
```

This preserves theoretical false positive guarantees while reducing hash computation cost to O(1).

### Bit storage

Bits are packed into a `ulong[]` array. Set and test operations use word-level indexing and bit masking — no per-operation allocations.

---

## Project structure

```
ByTech.BloomFilter.sln
├── src/
│   └── ByTech.BloomFilter/              # Core library
│       ├── Configuration/                # Parameter planning, validation
│       ├── Hashing/                      # Hash strategy, double-hashing
│       ├── Storage/                      # Packed ulong[] bit storage
│       ├── Serialization/                # Versioned binary format
│       └── Diagnostics/                  # Saturation metrics, snapshot
├── tests/
│   ├── ByTech.BloomFilter.Tests/         # Unit, integration, property-based, statistical
│   └── ByTech.BloomFilter.FuzzTests/     # Fuzz testing
├── benchmarks/
│   └── ByTech.BloomFilter.Benchmarks/    # BenchmarkDotNet suite
└── .docs/                                # Project documentation
```

---

## Design principles

**Benchmark-driven** — architectural decisions (hash strategy, capacity alignment, mapping method) are backed by measured results, not intuition.

**Correctness-first** — zero false negatives verified by property-based tests. Observed false positive rate validated against theoretical envelope using statistical tests with large sample sizes.

**Tiny hot path** — `Add` and `MayContain` are small, branch-light, allocation-free, and easy to inspect.

**Extension-ready** — internal design supports future counting, scalable, and concurrent variants without rewriting the core.

---

## Benchmarks

```bash
dotnet run --project benchmarks/ByTech.BloomFilter.Benchmarks -c Release
```

Benchmark suite includes: add throughput, query throughput (present/absent keys), mixed workloads, and serialization. All hot-path benchmarks confirm zero heap allocations via `[MemoryDiagnoser]`.

See [benchmark methodology](.docs/kb/benchmark-methodology.md) for environment requirements and interpretation rules.

---

## Thread safety

The v1 `BloomFilter` class is **not thread-safe**. If you need concurrent access, provide external synchronization. This is a deliberate design choice — see the [concurrency roadmap](.docs/kb/concurrency-roadmap.md) for future plans.

---

## Limitations

- **Not thread-safe** — v1 requires external synchronization for concurrent access
- **No deletion** — standard Bloom filter; use a counting variant for deletion support
- **No generic T** — v1 supports `ReadOnlySpan<byte>`, `string`, and `byte[]` only
- **Little-endian serialization** — binary format assumes little-endian host (standard for all .NET 10 platforms)
- **Maximum capacity** — 2^37 bits (~16 GB); exceeding this throws during construction

---

## Version

Current: **v1.0.0**

---

## License

Copyright 2026 ByTech. Author: Branimir Bajt. Licensed under the [Apache License, Version 2.0](LICENSE.MD).
