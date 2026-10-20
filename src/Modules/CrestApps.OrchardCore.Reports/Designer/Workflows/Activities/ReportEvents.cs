using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Reports.Designer.Workflows.Activities;

/// <summary>
/// A workflow event about a designed report, which can be limited to some reports. The workflow input holds
/// <c>ReportId</c>, <c>ReportTitle</c>, <c>UserName</c>, <c>Source</c> (<c>Admin</c>, <c>Shared</c> or <c>Link</c>),
/// <c>FilterValues</c>, and the values of the event: <c>RowCount</c>, <c>Format</c> or <c>VersionNumber</c>.
/// </summary>
public abstract class ReportEventActivity : EventActivity
{
    /// <summary>
    /// The workflow input that holds the id of the report.
    /// </summary>
    public const string ReportIdInput = "ReportId";

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportEventActivity"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected ReportEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc/>
    public override LocalizedString Category => S["Reports"];

    /// <summary>
    /// Gets or sets the reports that start the workflow; empty for every report.
    /// </summary>
    public IList<string> ReportIds
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Matches(ReportIds, workflowContext.Input);
    }

    /// <summary>
    /// Determines whether an event's report filter lets a report start the workflow.
    /// </summary>
    /// <param name="reportIds">The reports the event is limited to; empty for every report.</param>
    /// <param name="input">The workflow input, which holds the id of the report.</param>
    /// <returns><see langword="true"/> when the workflow starts.</returns>
    public static bool Matches(IList<string> reportIds, IDictionary<string, object> input)
    {
        return reportIds is null ||
            reportIds.Count == 0 ||
            (input is not null && input.TryGetValue(ReportIdInput, out var reportId) && reportIds.Contains(reportId as string, StringComparer.Ordinal));
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Done"])];
    }

    /// <inheritdoc/>
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcome("Done");
    }
}

/// <summary>
/// Starts a workflow when a person runs a report: on its page, on the shared reports page, or through a share link.
/// </summary>
public sealed class ReportRunEvent : ReportEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportRunEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportRunEvent(IStringLocalizer<ReportRunEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name => nameof(ReportRunEvent);

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Report Run"];
}

/// <summary>
/// Starts a workflow when a person exports a report.
/// </summary>
public sealed class ReportExportedEvent : ReportEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportExportedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportExportedEvent(IStringLocalizer<ReportExportedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name => nameof(ReportExportedEvent);

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Report Exported"];
}

/// <summary>
/// Starts a workflow when a person publishes a report.
/// </summary>
public sealed class ReportPublishedEvent : ReportEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportPublishedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportPublishedEvent(IStringLocalizer<ReportPublishedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name => nameof(ReportPublishedEvent);

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Report Published"];
}
