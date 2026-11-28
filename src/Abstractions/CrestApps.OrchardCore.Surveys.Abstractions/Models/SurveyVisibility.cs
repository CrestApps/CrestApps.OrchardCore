using CrestApps.OrchardCore.Conditions;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// When a page, question or choice shows.
/// </summary>
public sealed class SurveyVisibility
{
    /// <summary>
    /// Gets or sets whether it always shows, shows when the rule holds or hides when it holds.
    /// </summary>
    public VisibilityMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the rule; sources are question ids, question.row ids, var:name, score, score:category and context:key.
    /// </summary>
    public ConditionRule Rule { get; set; }

    /// <summary>
    /// Gets or sets an optional formula that must also hold; evaluated on the server only.
    /// </summary>
    public string Formula { get; set; }
}
