namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// What a designed report run is for, as its telemetry is tagged with: the bounded values of
/// <see cref="ReportTelemetry.RunSourceTag"/>. A source that is not one of them is tagged <see cref="Other"/>.
/// </summary>
public static class ReportRunSources
{
    /// <summary>
    /// A report page in the admin.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// A report shared with the people who visit it.
    /// </summary>
    public const string Shared = "Shared";

    /// <summary>
    /// A report opened through a share link.
    /// </summary>
    public const string Link = "Link";

    /// <summary>
    /// The designer preview.
    /// </summary>
    public const string Preview = "Preview";

    /// <summary>
    /// A dashboard or content widget.
    /// </summary>
    public const string Widget = "Widget";

    /// <summary>
    /// A workflow task.
    /// </summary>
    public const string Workflow = "Workflow";

    /// <summary>
    /// A scheduled report email.
    /// </summary>
    public const string Subscription = "Subscription";

    /// <summary>
    /// The refresh of a scheduled view's stored rows.
    /// </summary>
    public const string ViewRefresh = "ViewRefresh";

    /// <summary>
    /// A live view another report reads.
    /// </summary>
    public const string ViewRead = "ViewRead";

    /// <summary>
    /// A run whose caller does not name a source, or names one that is not listed here.
    /// </summary>
    public const string Other = "Other";

    private static readonly Dictionary<string, string> _known = new(StringComparer.OrdinalIgnoreCase)
    {
        [Admin] = Admin,
        [Shared] = Shared,
        [Link] = Link,
        [Preview] = Preview,
        [Widget] = Widget,
        [Workflow] = Workflow,
        [Subscription] = Subscription,
        [ViewRefresh] = ViewRefresh,
        [ViewRead] = ViewRead,
        [Other] = Other,
    };

    /// <summary>
    /// Maps a source to one of the values listed here, so a metric tag stays bounded.
    /// </summary>
    /// <param name="source">The source a caller named, or <see langword="null"/>.</param>
    /// <returns>The listed value, or <see cref="Other"/>.</returns>
    public static string Normalize(string source)
    {
        return !string.IsNullOrEmpty(source) && _known.TryGetValue(source, out var known)
            ? known
            : Other;
    }
}
