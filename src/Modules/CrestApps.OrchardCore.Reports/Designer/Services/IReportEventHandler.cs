using CrestApps.OrchardCore.Reports.Designer.Models;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Reacts to what people do with designed reports: running one, exporting one, and publishing one. Any number of
/// handlers can be registered, such as the one that starts workflows. The events come only from people's own requests,
/// never from a report that a workflow or another service runs, so a handler that runs a report cannot start itself
/// again.
/// </summary>
public interface IReportEventHandler
{
    /// <summary>
    /// Called after a person runs a report: on its page, on the shared reports page, or through a share link.
    /// </summary>
    /// <param name="context">What was run, by whom, and how.</param>
    /// <returns>A task that completes when the handler is done.</returns>
    Task RanAsync(ReportEventContext context);

    /// <summary>
    /// Called after a person exports a report.
    /// </summary>
    /// <param name="context">What was exported, by whom, how, and in which format.</param>
    /// <returns>A task that completes when the handler is done.</returns>
    Task ExportedAsync(ReportEventContext context);

    /// <summary>
    /// Called after a person publishes a report.
    /// </summary>
    /// <param name="context">What was published, by whom, and its version number.</param>
    /// <returns>A task that completes when the handler is done.</returns>
    Task PublishedAsync(ReportEventContext context);
}

/// <summary>
/// A base for <see cref="IReportEventHandler"/> implementations that handle only some events.
/// </summary>
public abstract class ReportEventHandlerBase : IReportEventHandler
{
    /// <inheritdoc/>
    public virtual Task RanAsync(ReportEventContext context) => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task ExportedAsync(ReportEventContext context) => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task PublishedAsync(ReportEventContext context) => Task.CompletedTask;
}

/// <summary>
/// How a person reached a report.
/// </summary>
public enum ReportEventSource
{
    /// <summary>
    /// The report's page in the admin, or the builder.
    /// </summary>
    Admin,

    /// <summary>
    /// The shared reports page, for people the report is shared with.
    /// </summary>
    Shared,

    /// <summary>
    /// A share link.
    /// </summary>
    Link,
}

/// <summary>
/// What happened to a report, for <see cref="IReportEventHandler"/>.
/// </summary>
public sealed class ReportEventContext
{
    /// <summary>
    /// Gets or sets the report.
    /// </summary>
    public ReportDesign Design { get; set; }

    /// <summary>
    /// Gets or sets the user name of the person, or <see langword="null"/> for a visitor who is not signed in.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets how the person reached the report.
    /// </summary>
    public ReportEventSource Source { get; set; }

    /// <summary>
    /// Gets or sets the number of rows the run returned, when the report was run.
    /// </summary>
    public int? RowCount { get; set; }

    /// <summary>
    /// Gets or sets the export format, such as <c>csv</c>, when the report was exported.
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets the version number, when the report was published and the publish kept a version.
    /// </summary>
    public int? VersionNumber { get; set; }

    /// <summary>
    /// Gets or sets the values of the report's filters that the person chose, by filter id.
    /// </summary>
    public IDictionary<string, IList<string>> FilterValues { get; set; } = new Dictionary<string, IList<string>>();
}
