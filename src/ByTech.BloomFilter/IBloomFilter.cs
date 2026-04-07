namespace ByTech.BloomFilter;

/// <summary>
/// Common interface for all Bloom filter implementations.
/// Provides the shared surface for Add, MayContain, and Clear operations.
/// </summary>
public interface IBloomFilter
{
    /// <summary>Number of items the filter is designed to hold.</summary>
    long ExpectedInsertions { get; }

    /// <summary>Target false positive probability the filter was configured for.</summary>
    double TargetFalsePositiveRate { get; }

    /// <summary>Total number of bit positions in the filter.</summary>
    long BitCount { get; }

    /// <summary>Number of hash functions (positions set per insertion).</summary>
    int HashFunctionCount { get; }

    /// <summary>
    /// Adds an item to the filter. After this call, <see cref="MayContain(ReadOnlySpan{byte})"/>
    /// will always return <c>true</c> for the same input.
    /// </summary>
    /// <param name="value">The item bytes to add.</param>
    void Add(ReadOnlySpan<byte> value);

    /// <summary>
    /// Adds a string item to the filter (UTF-8 encoded).
    /// </summary>
    /// <param name="value">The string to add.</param>
    void Add(string value);

    /// <summary>
    /// Tests whether an item may have been added to the filter.
    /// Returns <c>false</c> if the item was definitely not added.
    /// Returns <c>true</c> if the item was possibly added (may be a false positive).
    /// </summary>
    /// <param name="value">The item bytes to test.</param>
    /// <returns><c>true</c> if possibly present; <c>false</c> if definitely absent.</returns>
    bool MayContain(ReadOnlySpan<byte> value);

    /// <summary>
    /// Tests whether a string item may have been added (UTF-8 encoded).
    /// </summary>
    /// <param name="value">The string to test.</param>
    /// <returns><c>true</c> if possibly present; <c>false</c> if definitely absent.</returns>
    bool MayContain(string value);

    /// <summary>
    /// Adds multiple items to the filter.
    /// </summary>
    /// <param name="items">The items to add.</param>
    void AddRange(ReadOnlyMemory<byte>[] items);

    /// <summary>
    /// Tests whether all items may have been added to the filter.
    /// Returns <c>false</c> as soon as any item is definitely absent.
    /// </summary>
    /// <param name="items">The items to test.</param>
    /// <returns><c>true</c> if all items are possibly present; <c>false</c> if any is definitely absent.</returns>
    bool ContainsAll(ReadOnlyMemory<byte>[] items);

    /// <summary>
    /// Tests whether at least one item may have been added to the filter.
    /// Returns <c>true</c> as soon as any item is possibly present.
    /// </summary>
    /// <param name="items">The items to test.</param>
    /// <returns><c>true</c> if any item is possibly present; <c>false</c> if all are definitely absent.</returns>
    bool ContainsAny(ReadOnlyMemory<byte>[] items);

    /// <summary>
    /// Resets the filter to its initial empty state.
    /// </summary>
    void Clear();
}
