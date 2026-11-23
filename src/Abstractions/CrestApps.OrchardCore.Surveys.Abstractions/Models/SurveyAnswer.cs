using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// The answer to one question, and its score.
/// </summary>
public sealed class SurveyAnswer
{
    /// <summary>
    /// Gets or sets the chosen choice ids or the entered text.
    /// </summary>
    public List<string> Values { get; set; } = [];

    /// <summary>
    /// Gets or sets the entered number.
    /// </summary>
    public decimal? Number { get; set; }

    /// <summary>
    /// Gets or sets the entered date and time.
    /// </summary>
    public DateTime? Date { get; set; }

    /// <summary>
    /// Gets or sets the values of each row by row id (matrix, matching, multiple text).
    /// </summary>
    public Dictionary<string, List<string>> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets the text of an other choice.
    /// </summary>
    public string OtherText { get; set; }

    /// <summary>
    /// Gets or sets the ids of uploaded files.
    /// </summary>
    public List<string> AttachmentIds { get; set; } = [];

    /// <summary>
    /// Gets or sets control-specific data.
    /// </summary>
    public JsonObject Extra { get; set; } = [];

    /// <summary>
    /// Gets or sets when it was last answered, in UTC.
    /// </summary>
    public DateTime AnsweredUtc { get; set; }

    /// <summary>
    /// Gets or sets the score the scorer gave.
    /// </summary>
    public decimal? AutoScore { get; set; }

    /// <summary>
    /// Gets or sets the score a grader gave; it wins over the automatic score.
    /// </summary>
    public decimal? ManualScore { get; set; }

    /// <summary>
    /// Gets or sets the most points possible.
    /// </summary>
    public decimal? MaxPoints { get; set; }

    /// <summary>
    /// Gets or sets the grader's comment.
    /// </summary>
    public string GraderComment { get; set; }

    /// <summary>
    /// Gets or sets whether the grader used AI help.
    /// </summary>
    public bool AIAssisted { get; set; }

    /// <summary>
    /// Gets or sets when the respondent checked the answer with immediate feedback; it is then locked.
    /// </summary>
    public DateTime? CheckedUtc { get; set; }

    /// <summary>
    /// Gets whether the answer is locked: the respondent checked it with immediate feedback and saw it graded, so it is
    /// kept, and it counts toward the score, even when logic or a branch later hides its question.
    /// </summary>
    [JsonIgnore]
    public bool IsLocked => CheckedUtc is not null;

    /// <summary>
    /// Gets the score that counts: the grader's when there is one, else the automatic one.
    /// </summary>
    [JsonIgnore]
    public decimal? Score => ManualScore ?? AutoScore;
}
