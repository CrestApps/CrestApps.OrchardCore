using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Learning.Core.Telemetry;

/// <summary>
/// The meter and activity source of Learning, <c>CrestApps.OrchardCore.Learning</c>: enrollments, status changes,
/// completed items and courses, notices, and the daily upkeep. User, course and enrollment ids go on spans and logs
/// only, never on a metric.
/// </summary>
public static class LearningTelemetry
{
    /// <summary>
    /// The name of the Learning meter and activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Learning";

    /// <summary>
    /// The name of the span of one upkeep run.
    /// </summary>
    public const string UpkeepSpanName = "crestapps.learning.upkeep";

    /// <summary>
    /// The tag that records the source of the first grant of an enrollment, such as <c>Manual</c> or <c>Self</c>.
    /// </summary>
    public const string SourceTag = "source";

    /// <summary>
    /// The tag that records the state an enrollment left.
    /// </summary>
    public const string FromTag = "from";

    /// <summary>
    /// The tag that records the state an enrollment moved to.
    /// </summary>
    public const string ToTag = "to";

    /// <summary>
    /// The tag that records who completed an item: <c>learner</c> or <c>staff</c>.
    /// </summary>
    public const string CompletedByTag = "completed_by";

    /// <summary>
    /// The tag that records the kind of a notice: <c>access_ending</c>, <c>due_soon</c>, <c>overdue</c> or <c>inactive</c>.
    /// </summary>
    public const string NoticeTag = "notice";

    /// <summary>
    /// The meter of Learning.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Learning");

    /// <summary>
    /// The activity source of Learning.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Learning");

    /// <summary>
    /// The number of enrollments created, tagged with <see cref="SourceTag"/>.
    /// </summary>
    public static Counter<long> Enrollments { get; } = Meter.CreateCounter<long>(
        "crestapps.learning.enrollments",
        "{enrollment}",
        "The number of enrollments created.");

    /// <summary>
    /// The tag of how an attempt was submitted or graded: <c>learner</c>, <c>deadline</c>, <c>automatic</c>, <c>staff</c>.
    /// </summary>
    public const string ByTag = "by";

    /// <summary>
    /// The tag of the kind of a score change: <c>Overridden</c>, <c>Regraded</c>, <c>Voided</c>.
    /// </summary>
    public const string ChangeTag = "change";

    /// <summary>
    /// The number of quiz attempts started.
    /// </summary>
    public static Counter<long> AttemptsStarted { get; } = Meter.CreateCounter<long>(
        "crestapps.learning.attempts.started",
        "{attempt}",
        "The number of quiz attempts started.");

    /// <summary>
    /// The number of quiz attempts submitted, tagged with <see cref="ByTag"/> (learner or deadline).
    /// </summary>
    public static Counter<long> AttemptsSubmitted { get; } = Meter.CreateCounter<long>(
        "crestapps.learning.attempts.submitted",
        "{attempt}",
        "The number of quiz attempts submitted.");

    /// <summary>
    /// The number of quiz attempts graded, tagged with <see cref="ByTag"/> (automatic or staff).
    /// </summary>
    public static Counter<long> AttemptsGraded { get; } = Meter.CreateCounter<long>(
        "crestapps.learning.attempts.graded",
        "{attempt}",
        "The number of quiz attempts graded.");

    /// <summary>
    /// The number of quiz score changes after grading, tagged with <see cref="ChangeTag"/>.
    /// </summary>
    public static Counter<long> ScoreChanges { get; } = Meter.CreateCounter<long>(
        "crestapps.learning.attempts.score_changes",
        "{change}",
        "The number of quiz scores overridden, regraded or voided.");

    /// <summary>
    /// The number of enrollment status changes, tagged with <see cref="FromTag"/> and <see cref="ToTag"/>.
    /// </summary>
    public static Counter<long> StatusChanges { get; } = Meter.CreateCounter<long>(
        "crestapps.learning.enrollment.status_changes",
        "{change}",
        "The number of enrollment status changes.");

    /// <summary>
    /// The number of course items completed, tagged with <see cref="CompletedByTag"/>.
    /// </summary>
    public static Counter<long> ItemsCompleted { get; } = Meter.CreateCounter<long>(
        "crestapps.learning.items.completed",
        "{item}",
        "The number of course items learners completed, or staff completed for them.");

    /// <summary>
    /// The number of courses completed, tagged with <see cref="SourceTag"/>.
    /// </summary>
    public static Counter<long> CoursesCompleted { get; } = Meter.CreateCounter<long>(
        "crestapps.learning.courses.completed",
        "{course}",
        "The number of courses learners completed.");

    /// <summary>
    /// The number of notices the upkeep raised, tagged with <see cref="NoticeTag"/>.
    /// </summary>
    public static Counter<long> CertificationChanges { get; } = Meter.CreateCounter<long>(
        "crestapps.learning.certification.changes",
        unit: "{change}",
        description: "Certification path changes: certificates issued and renewed, renewals opened, lapses and revocations.");

    public static Counter<long> Notices { get; } = Meter.CreateCounter<long>(
        "crestapps.learning.notices",
        "{notice}",
        "The number of access-ending, due-soon, overdue and inactive notices the upkeep raised.");

    /// <summary>
    /// The duration of an upkeep run, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>.
    /// </summary>
    public static Histogram<double> UpkeepDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.learning.upkeep.duration",
        "The duration of one run of the daily Learning upkeep.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// Starts measuring an upkeep run.
    /// </summary>
    /// <returns>The scope of the run.</returns>
    public static TelemetryScope StartUpkeep()
        => TelemetryScope.Start(ActivitySource, UpkeepSpanName, UpkeepDuration);

    /// <summary>
    /// Adds one to a counter with one tag.
    /// </summary>
    /// <param name="counter">The counter.</param>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    public static void Add(Counter<long> counter, string key, string value)
    {
        if (!counter.Enabled)
        {
            return;
        }

        var tags = new TagList { { key, value ?? CrestAppsOrchardCoreTelemetry.Unknown } };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        counter.Add(1, tags);
    }

    /// <summary>
    /// Adds one to the status change counter.
    /// </summary>
    /// <param name="from">The state the enrollment left.</param>
    /// <param name="to">The state it moved to.</param>
    public static void AddStatusChange(string from, string to)
    {
        if (!StatusChanges.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { FromTag, from },
            { ToTag, to },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        StatusChanges.Add(1, tags);
    }
}
