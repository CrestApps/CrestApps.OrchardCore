using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// The state of reading one answer from a request.
/// </summary>
public sealed class SurveyAnswerReadContext
{
    /// <summary>
    /// Gets or sets the question.
    /// </summary>
    public SurveyQuestion Question { get; set; }

    /// <summary>
    /// Gets or sets the response being answered.
    /// </summary>
    public SurveyResponse Response { get; set; }

    /// <summary>
    /// Gets or sets the definition of the response's version.
    /// </summary>
    public SurveyDefinition Definition { get; set; }

    /// <summary>
    /// Gets or sets the posted form, or <see langword="null"/> for a JSON request.
    /// </summary>
    public IFormCollection Form { get; set; }

    /// <summary>
    /// Gets or sets the posted JSON value of the answer, or <see langword="null"/> for a form request.
    /// </summary>
    public JsonNode Json { get; set; }

    /// <summary>
    /// Gets or sets the input name prefix of the question: <c>q.{questionId}</c>.
    /// </summary>
    public string Prefix { get; set; }

    /// <summary>
    /// Gets or sets the answer saved before, or <see langword="null"/>.
    /// </summary>
    public SurveyAnswer Existing { get; set; }

    /// <summary>
    /// Gets or sets the answer read; the handler sets it, or leaves it <see langword="null"/> for no answer.
    /// </summary>
    public SurveyAnswer Answer { get; set; }

    /// <summary>
    /// Gets or sets the choice ids the respondent may pick, in plan order (visible, not retired).
    /// </summary>
    public IReadOnlyList<string> AllowedChoiceIds { get; set; } = [];

    /// <summary>
    /// Gets or sets errors found while reading, such as an upload refused.
    /// </summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets or sets the cancellation token.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }
}
