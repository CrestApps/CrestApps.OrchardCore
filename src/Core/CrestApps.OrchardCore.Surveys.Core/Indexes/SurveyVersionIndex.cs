using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Finds the versions of a survey.
/// </summary>
public sealed class SurveyVersionIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the version number.
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets when the version was published, in UTC.
    /// </summary>
    public DateTime PublishedUtc { get; set; }

    /// <summary>
    /// The longest notes the index keeps; longer notes end with an ellipsis in the versions list.
    /// </summary>
    public const int NotesLength = 500;

    /// <summary>
    /// Gets or sets the id of the user who published the version, or an empty string when nobody is recorded. Null on a
    /// row indexed before the column existed: the versions list then reads that version itself until the step that
    /// indexes the versions again reaches it.
    /// </summary>
    public string PublishedBy { get; set; }

    /// <summary>
    /// Gets or sets the version's notes, cut to <see cref="NotesLength"/> characters, or null.
    /// </summary>
    public string Notes { get; set; }
}
