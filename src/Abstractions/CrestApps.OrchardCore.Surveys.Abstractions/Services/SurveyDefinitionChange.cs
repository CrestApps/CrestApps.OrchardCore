namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// One change between two survey definitions.
/// </summary>
public sealed class SurveyDefinitionChange
{
    /// <summary>
    /// Gets or sets what changed.
    /// </summary>
    public SurveyChangeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the kind of item changed: page, question, choice, row, ending, variable or settings.
    /// </summary>
    public string Target { get; set; }

    /// <summary>
    /// Gets or sets the id of the item.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name or text of the item.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the id of the question the item belongs to, for choices and rows.
    /// </summary>
    public string QuestionId { get; set; }

    /// <summary>
    /// Gets or sets what changed, such as control type or scoring key.
    /// </summary>
    public string Detail { get; set; }

    /// <summary>
    /// Gets or sets whether the change may affect existing answers or comparisons.
    /// </summary>
    public bool Destructive { get; set; }
}
