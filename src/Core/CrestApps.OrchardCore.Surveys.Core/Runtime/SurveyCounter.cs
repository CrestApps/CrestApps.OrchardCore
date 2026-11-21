using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Counts of one survey kept by the runtime, such as how many times each pool question was presented or how many
/// completed responses each quota holds. Changed only under the survey's lock.
/// </summary>
public sealed class SurveyCounter : CatalogItem
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets what the counter counts (see <see cref="SurveyCounterKinds"/>).
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the counts by key, such as a question id or a quota id.
    /// </summary>
    public Dictionary<string, int> Counts { get; set; } = [];

    /// <summary>
    /// Gets or sets when the counter last changed, in UTC.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }
}
