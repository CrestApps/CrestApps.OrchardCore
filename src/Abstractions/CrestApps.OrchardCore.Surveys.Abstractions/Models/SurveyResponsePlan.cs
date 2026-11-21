namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// What one response shows and in which order, fixed when it starts so refresh, back and resume show the same.
/// </summary>
public sealed class SurveyResponsePlan
{
    /// <summary>
    /// Gets or sets the seed of the random draw.
    /// </summary>
    public int Seed { get; set; }

    /// <summary>
    /// Gets or sets the ids of the pages in order (screens when questions are split one per page).
    /// </summary>
    public List<string> PageOrder { get; set; } = [];

    /// <summary>
    /// Gets or sets the ids of the questions shown on each page, by page id.
    /// </summary>
    public Dictionary<string, List<string>> QuestionOrder { get; set; } = [];

    /// <summary>
    /// Gets or sets the ids of the choices in display order, by question id.
    /// </summary>
    public Dictionary<string, List<string>> ChoiceOrder { get; set; } = [];

    /// <summary>
    /// Gets or sets the designed page of each screen when pages are split.
    /// </summary>
    public Dictionary<string, string> ScreenPages { get; set; } = [];
}
