namespace CrestApps.OrchardCore.Diagnostics;

/// <summary>
/// One run of a background task measured by <see cref="BackgroundTaskTelemetry"/>. The work reports on it what it did:
/// the items it processed, that it skipped its work, or a failure it logged and swallowed.
/// </summary>
public sealed class MeasuredTaskRun
{
    private readonly Dictionary<string, long> _items = new(StringComparer.Ordinal);

    internal MeasuredTaskRun(string taskName)
    {
        TaskName = taskName;
    }

    /// <summary>
    /// Gets the name of the task.
    /// </summary>
    public string TaskName { get; }

    /// <summary>
    /// Gets how the run ended. It is <see cref="CrestAppsOrchardCoreTelemetry.OutcomeOk"/> unless the work reports otherwise.
    /// </summary>
    public string Outcome { get; private set; } = CrestAppsOrchardCoreTelemetry.OutcomeOk;

    /// <summary>
    /// Gets the items the run processed, by what became of them.
    /// </summary>
    public IReadOnlyDictionary<string, long> Items => _items;

    /// <summary>
    /// Adds items the run processed.
    /// </summary>
    /// <param name="count">The number of items. Zero and negative counts are ignored.</param>
    /// <param name="result">What became of them, from a bounded set, such as <c>processed</c>, <c>failed</c> or <c>skipped</c>.</param>
    public void AddItems(long count, string result = "processed")
    {
        if (count <= 0)
        {
            return;
        }

        result ??= CrestAppsOrchardCoreTelemetry.Unknown;
        _items[result] = _items.TryGetValue(result, out var current)
            ? current + count
            : count;
    }

    /// <summary>
    /// Records that the run did nothing because there was nothing due, or the feature it serves is off.
    /// </summary>
    public void Skip()
    {
        Outcome = CrestAppsOrchardCoreTelemetry.OutcomeSkipped;
    }

    /// <summary>
    /// Records that the run did nothing because another node or worker holds the work lease.
    /// </summary>
    public void SkipForLease()
    {
        Outcome = BackgroundTaskTelemetry.OutcomeSkippedLease;
    }

    /// <summary>
    /// Records that the run stopped before it finished, because it used up the time or item budget it allows itself.
    /// </summary>
    public void BudgetExceeded()
    {
        if (Outcome == CrestAppsOrchardCoreTelemetry.OutcomeOk)
        {
            Outcome = BackgroundTaskTelemetry.OutcomeBudgetExceeded;
        }
    }

    /// <summary>
    /// Records that the run failed, for a task that logs and swallows the failure rather than throwing it.
    /// </summary>
    /// <param name="exception">The exception the run swallowed, or <see langword="null"/>.</param>
    public void ReportError(Exception exception = null)
    {
        if (exception is not null)
        {
            SetOutcomeFromException(exception);

            return;
        }

        Outcome = CrestAppsOrchardCoreTelemetry.OutcomeError;
    }

    internal void SetOutcomeFromException(Exception exception)
    {
        Outcome = CrestAppsOrchardCoreTelemetry.GetOutcome(exception);
    }
}
