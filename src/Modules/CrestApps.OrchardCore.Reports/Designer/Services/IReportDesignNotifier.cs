namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Tells the people who have a report open in the builder that it changed. The default does nothing; when
/// <c>OrchardCore.SignalR</c> is enabled, the change is sent over the reports hub.
/// </summary>
public interface IReportDesignNotifier
{
    /// <summary>
    /// Announces a change of a report. Implementations send it only once the change is committed.
    /// </summary>
    /// <param name="change">The change.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReportDesignChangedAsync(ReportDesignChange change);
}

/// <summary>
/// What happened to a designed report.
/// </summary>
public enum ReportDesignChangeKind
{
    /// <summary>
    /// Its draft was saved.
    /// </summary>
    DraftSaved,

    /// <summary>
    /// Its draft was discarded.
    /// </summary>
    DraftDiscarded,

    /// <summary>
    /// It was published.
    /// </summary>
    Published,

    /// <summary>
    /// A version was restored into its draft.
    /// </summary>
    Restored,

    /// <summary>
    /// It was deleted.
    /// </summary>
    Deleted,
}

/// <summary>
/// A change of a designed report.
/// </summary>
public sealed class ReportDesignChange
{
    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public ReportDesignChangeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the report.
    /// </summary>
    public string DesignId { get; set; }

    /// <summary>
    /// Gets or sets the revision of the report after the change.
    /// </summary>
    public long Revision { get; set; }

    /// <summary>
    /// Gets or sets the version the change published or restored, if any.
    /// </summary>
    public int? VersionNumber { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who made the change.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the name of the user who made the change.
    /// </summary>
    public string UserName { get; set; }
}

/// <summary>
/// The notifier used when real-time updates are off.
/// </summary>
public sealed class NullReportDesignNotifier : IReportDesignNotifier
{
    /// <inheritdoc/>
    public Task ReportDesignChangedAsync(ReportDesignChange change)
    {
        return Task.CompletedTask;
    }
}
