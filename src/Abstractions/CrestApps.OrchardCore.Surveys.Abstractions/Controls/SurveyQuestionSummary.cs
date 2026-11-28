using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// The summary of the answers to one question.
/// </summary>
public sealed class SurveyQuestionSummary
{
    /// <summary>
    /// Gets or sets the id of the question.
    /// </summary>
    public string QuestionId { get; set; }

    /// <summary>
    /// Gets or sets how many responses answered it.
    /// </summary>
    public long Answered { get; set; }

    /// <summary>
    /// Gets or sets how many responses were counted.
    /// </summary>
    public long Responses { get; set; }

    /// <summary>
    /// Gets or sets how many times each choice was chosen, by choice id.
    /// </summary>
    public Dictionary<string, long> ChoiceCounts { get; set; } = [];

    /// <summary>
    /// Gets or sets how many times each choice was chosen in each row: row id, then choice id.
    /// </summary>
    public Dictionary<string, Dictionary<string, long>> RowChoiceCounts { get; set; } = [];

    /// <summary>
    /// Gets or sets the statistics of number values, or <see langword="null"/>.
    /// </summary>
    public SurveyNumberStatistics Numbers { get; set; }

    /// <summary>
    /// Gets or sets a sample of the text answers.
    /// </summary>
    public List<string> TextSamples { get; set; } = [];

    /// <summary>
    /// Gets or sets data of custom analyzers.
    /// </summary>
    public JsonObject Extra { get; set; } = [];
}
