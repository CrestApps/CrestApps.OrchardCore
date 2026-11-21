using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// A published, immutable version of a survey's definition. Responses are pinned to the version they started on.
/// </summary>
public sealed class SurveyVersion : CatalogItem
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the number: 1, 2, 3 and so on.
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets the definition, with bank draws resolved. It never changes.
    /// </summary>
    public SurveyDefinition Definition { get; set; }

    /// <summary>
    /// Gets or sets when the version was published, in UTC.
    /// </summary>
    public DateTime PublishedUtc { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who published it.
    /// </summary>
    public string PublishedBy { get; set; }

    /// <summary>
    /// Gets or sets the publish notes.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 of the canonical definition JSON.
    /// </summary>
    public string ContentHash { get; set; }
}
