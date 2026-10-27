namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// What a run of a designed report shows besides its filters: the values people picked by clicking its visuals, and
/// the visuals to refresh when only some of them change.
/// </summary>
public sealed class ReportRunOptions
{
    /// <summary>
    /// Gets or sets the values picked in the visuals people can click, as keys by visual identifier. Each one filters
    /// the visuals its visual's click action names.
    /// </summary>
    public IDictionary<string, IList<string>> Selections { get; set; } = new Dictionary<string, IList<string>>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the identifiers of the visuals to show, or <see langword="null"/> to show every visual. Only the
    /// queries those visuals show run.
    /// </summary>
    public ISet<string> VisualIds { get; set; }
}
