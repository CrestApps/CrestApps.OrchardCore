using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Finds surveys by name, kind, status, owner and change time, and holds what lists and access checks read of a survey
/// (its user owner and its anonymity), so they never load survey documents.
/// </summary>
public sealed class SurveyIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the unique technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title, cut to 255 characters.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the owner as Kind:Id, or null.
    /// </summary>
    public string OwnerRef { get; set; }

    /// <summary>
    /// Gets or sets the published version number.
    /// </summary>
    public int PublishedVersionNumber { get; set; }

    /// <summary>
    /// Gets or sets when the survey last changed (or was created, when it never changed), in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who owns the survey, or null.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the anonymity mode. Null on a row indexed before the column existed, which is then read from the
    /// survey itself.
    /// </summary>
    public string Anonymity { get; set; }

    /// <summary>
    /// Gets or sets the survey's own minimum group size, or null.
    /// </summary>
    public int? MinimumGroupSize { get; set; }

    /// <summary>
    /// Gets or sets how many days responses are kept before retention removes or anonymizes them, or 0 when the survey
    /// has no retention. Null on a row indexed before the upkeep columns existed: upkeep then reads that survey itself
    /// until the step that indexes the surveys again reaches it.
    /// </summary>
    public int? RetentionDays { get; set; }

    /// <summary>
    /// Gets or sets the survey's own closing date, in UTC, or null.
    /// </summary>
    public DateTime? ClosesUtc { get; set; }

    /// <summary>
    /// Gets or sets the most responses a respondent may give in total, or null; participation records of such a survey
    /// are kept.
    /// </summary>
    public int? TotalLimit { get; set; }

    /// <summary>
    /// Gets or sets when staff started deleting the survey, in UTC, or null (see <c>Survey.DeletingUtc</c>), so a start
    /// reads the mark by the index, without loading the survey.
    /// </summary>
    public DateTime? DeletingUtc { get; set; }
}
