using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;

namespace CrestApps.OrchardCore.Timeline.Core.Services;

/// <summary>
/// Deletes the timelines of removed records, with their entries' files, after the removal committed: the removal asks
/// for it in its own transaction (<see cref="RequestAsync"/>), so the request can't be lost, and a run that fails is run
/// again on the next pass instead of leaving the entries and files behind.
/// </summary>
public sealed class TimelineDeletionStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Timeline.DeleteRecords";

    // The records whose timelines wait to be deleted are the step's data, one key each.
    private const string SubjectPrefix = "subject:";
    private const int RecordsPerRun = 50;

    private readonly ITimelineService _timeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineDeletionStep"/> class.
    /// </summary>
    public TimelineDeletionStep(ITimelineService timeline)
    {
        _timeline = timeline;
    }

    /// <inheritdoc/>
    public string Name => StepName;

    /// <summary>
    /// Asks for a record's timeline to be deleted, in the current transaction.
    /// </summary>
    /// <param name="steps">The resumable steps.</param>
    /// <param name="subject">The record.</param>
    /// <returns>A task.</returns>
    public static Task RequestAsync(ResumableSteps steps, RecordRef subject)
    {
        ArgumentNullException.ThrowIfNull(steps);

        return subject.IsEmpty
            ? Task.CompletedTask
            : steps.RequestAsync(StepName, new Dictionary<string, string> { [SubjectPrefix + subject] = "1" });
    }

    /// <inheritdoc/>
    public async Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var key in context.Data.Keys.Where(key => key.StartsWith(SubjectPrefix, StringComparison.Ordinal)).Take(RecordsPerRun).ToList())
        {
            if (RecordRef.TryParse(key[SubjectPrefix.Length..], out var subject))
            {
                await _timeline.DeleteAllAsync(subject, cancellationToken);
            }

            context.Data.Remove(key);
        }

        return !context.Data.Keys.Any(key => key.StartsWith(SubjectPrefix, StringComparison.Ordinal));
    }
}
