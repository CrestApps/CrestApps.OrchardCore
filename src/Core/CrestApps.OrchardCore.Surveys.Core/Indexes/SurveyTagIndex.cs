using YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// One row per tag of a survey, so the survey list filters by tag and lists the tags in use in the database.
/// </summary>
public sealed class SurveyTagIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the survey id.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the tag, trimmed and cut to 100 characters.
    /// </summary>
    public string Tag { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who owns the survey, or null, so a user who sees only their own surveys sees only
    /// their tags.
    /// </summary>
    public string OwnerId { get; set; }
}
