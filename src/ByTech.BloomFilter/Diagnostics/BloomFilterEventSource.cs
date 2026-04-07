using System.Diagnostics.Tracing;

namespace ByTech.BloomFilter.Diagnostics;

/// <summary>
/// ETW EventSource for Bloom filter operations. Provides opt-in counters observable
/// via <c>dotnet-counters</c>, PerfView, or Application Insights.
/// </summary>
/// <remarks>
/// Zero cost when no listener is attached — all event methods are guarded by
/// <see cref="EventSource.IsEnabled()"/> which the JIT can inline and eliminate.
/// No external dependencies.
/// </remarks>
[EventSource(Name = "ByTech.BloomFilter")]
internal sealed class BloomFilterEventSource : EventSource
{
    /// <summary>Singleton instance.</summary>
    public static readonly BloomFilterEventSource Instance = new();

    private IncrementingEventCounter? _itemsAddedCounter;
    private IncrementingEventCounter? _queriesCounter;
    private EventCounter? _falsePositiveEstimateCounter;

    private BloomFilterEventSource() : base(EventSourceSettings.EtwSelfDescribingEventFormat)
    {
    }

    /// <summary>
    /// Records that an item was added to a filter.
    /// </summary>
    public void ItemAdded()
    {
        if (IsEnabled())
        {
            _itemsAddedCounter?.Increment();
        }
    }

    /// <summary>
    /// Records that a membership query was performed.
    /// </summary>
    public void QueryPerformed()
    {
        if (IsEnabled())
        {
            _queriesCounter?.Increment();
        }
    }

    /// <summary>
    /// Reports the estimated false positive rate from a filter snapshot.
    /// Call this periodically (e.g., after snapshot) to update the gauge.
    /// </summary>
    /// <param name="estimatedFpr">The estimated current false positive rate.</param>
    public void ReportFalsePositiveEstimate(double estimatedFpr)
    {
        if (IsEnabled())
        {
            _falsePositiveEstimateCounter?.WriteMetric(estimatedFpr);
        }
    }

    /// <inheritdoc />
    protected override void OnEventCommand(EventCommandEventArgs command)
    {
        if (command.Command == EventCommand.Enable)
        {
            _itemsAddedCounter ??= new IncrementingEventCounter("bloom.items_added", this)
            {
                DisplayName = "Items Added",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1),
            };

            _queriesCounter ??= new IncrementingEventCounter("bloom.queries", this)
            {
                DisplayName = "Queries",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1),
            };

            _falsePositiveEstimateCounter ??= new EventCounter("bloom.false_positive_estimate", this)
            {
                DisplayName = "Estimated FPR",
            };
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _itemsAddedCounter?.Dispose();
        _queriesCounter?.Dispose();
        _falsePositiveEstimateCounter?.Dispose();
        base.Dispose(disposing);
    }
}
