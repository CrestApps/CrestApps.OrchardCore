using CrestApps.OrchardCore.Reports.Designer.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Reports.Designer.Indexes;

/// <summary>
/// Finds the draft of a designed report.
/// </summary>
public sealed class ReportDesignDraftIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the identifier of the report.
    /// </summary>
    public string DesignId { get; set; }
}

/// <summary>
/// Lists the versions of a designed report without loading them.
/// </summary>
public sealed class ReportDesignVersionIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the identifier of the report.
    /// </summary>
    public string DesignId { get; set; }

    /// <summary>
    /// Gets or sets the version number.
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets the report title at that version.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets when the version was published, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the name of the user who published the version.
    /// </summary>
    public string CreatedByName { get; set; }

    /// <summary>
    /// Gets or sets the number of the version it was restored from, if any.
    /// </summary>
    public int? RestoredFrom { get; set; }
}

/// <summary>
/// Maps report drafts to <see cref="ReportDesignDraftIndex"/>.
/// </summary>
public sealed class ReportDesignDraftIndexProvider : IndexProvider<ReportDesignDraft>
{
    /// <inheritdoc/>
    public override void Describe(DescribeContext<ReportDesignDraft> context)
    {
        context
            .For<ReportDesignDraftIndex>()
            .Map(draft => new ReportDesignDraftIndex
            {
                DesignId = draft.DesignId,
            });
    }
}

/// <summary>
/// Maps report versions to <see cref="ReportDesignVersionIndex"/>.
/// </summary>
public sealed class ReportDesignVersionIndexProvider : IndexProvider<ReportDesignVersion>
{
    /// <summary>
    /// The longest report title kept in the index.
    /// </summary>
    public const int MaxDisplayTextLength = 255;

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ReportDesignVersion> context)
    {
        context
            .For<ReportDesignVersionIndex>()
            .Map(version => new ReportDesignVersionIndex
            {
                DesignId = version.DesignId,
                Number = version.Number,
                DisplayText = Truncate(version.Design?.DisplayText, MaxDisplayTextLength),
                CreatedUtc = version.CreatedUtc,
                CreatedByName = Truncate(version.CreatedByName, MaxDisplayTextLength),
                RestoredFrom = version.RestoredFrom,
            });
    }

    private static string Truncate(string value, int length)
    {
        return value is null || value.Length <= length ? value : value[..length];
    }
}
