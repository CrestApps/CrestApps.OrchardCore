namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// The filters of the survey list.
/// </summary>
public sealed class SurveyListQuery
{
    /// <summary>
    /// Gets or sets a search on the title and the name.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the kind, or <see langword="null"/> for all.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the status, or <see langword="null"/> for all but archived.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets whether surveys owned by other records (such as course items) are listed too.
    /// </summary>
    public bool IncludeOwned { get; set; }

    /// <summary>
    /// Gets or sets the id of a user whose surveys are listed, or <see langword="null"/> for all.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets a tag the surveys must have, or <see langword="null"/>.
    /// </summary>
    public string Tag { get; set; }
}
