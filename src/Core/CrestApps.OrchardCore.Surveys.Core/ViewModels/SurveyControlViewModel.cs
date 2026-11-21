using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.ViewModels;

/// <summary>
/// What a question type's template renders: the respondent view of the question, its answer, errors and feedback.
/// </summary>
public class SurveyControlViewModel
{
    /// <summary>
    /// Gets or sets the respondent view of the question (no key).
    /// </summary>
    public SurveyQuestionView Question { get; set; }

    /// <summary>
    /// Gets or sets the settings respondents may see.
    /// </summary>
    public JsonObject Settings { get; set; } = [];

    /// <summary>
    /// Gets or sets the answer, or <see langword="null"/>.
    /// </summary>
    public SurveyAnswer Answer { get; set; }

    /// <summary>
    /// Gets or sets the answer as plain text, for read-only displays.
    /// </summary>
    public string AnswerText { get; set; }

    /// <summary>
    /// Gets or sets the input name prefix: <c>q.{questionId}</c>.
    /// </summary>
    public string Prefix { get; set; }

    /// <summary>
    /// Gets or sets the visible choices in plan order.
    /// </summary>
    public IReadOnlyList<SurveyChoiceView> Choices { get; set; } = [];

    /// <summary>
    /// Gets or sets the errors of the answer.
    /// </summary>
    public IReadOnlyList<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the question renders read-only.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Gets or sets the feedback the review policy allows, or <see langword="null"/>.
    /// </summary>
    public SurveyAnswerFeedback Feedback { get; set; }

    /// <summary>
    /// Gets or sets the culture the question is presented in.
    /// </summary>
    public string Culture { get; set; }

    /// <summary>
    /// Gets or sets the summary of the answers, for the results display, or <see langword="null"/>.
    /// </summary>
    public SurveyQuestionSummary Summary { get; set; }

    /// <summary>
    /// Gets the id of the element that holds the errors, for <c>aria-describedby</c>.
    /// </summary>
    public string ErrorId => $"{InputId}-errors";

    /// <summary>
    /// Gets the id of the element that holds the description, for <c>aria-describedby</c>.
    /// </summary>
    public string HintId => $"{InputId}-hint";

    /// <summary>
    /// Gets an HTML id derived from the prefix.
    /// </summary>
    public string InputId => (Prefix ?? "q").Replace('.', '-');

    /// <summary>
    /// Gets whether there are errors.
    /// </summary>
    public bool HasErrors => Errors?.Count > 0;

    /// <summary>
    /// Reads the settings as a typed object.
    /// </summary>
    /// <typeparam name="TSettings">The settings type.</typeparam>
    /// <returns>The settings.</returns>
    public TSettings GetSettings<TSettings>()
        where TSettings : class, new()
    {
        if (Settings is not { Count: > 0 })
        {
            return new TSettings();
        }

        try
        {
            return Settings.Deserialize<TSettings>(SurveyJson.Options) ?? new TSettings();
        }
        catch (JsonException)
        {
            return new TSettings();
        }
    }

    /// <summary>
    /// Tells whether a value is among the answer's values.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public bool IsSelected(string value)
        => Answer?.Values?.Contains(value, StringComparer.Ordinal) == true;

    /// <summary>
    /// Tells whether a value is among the values of a row.
    /// </summary>
    /// <param name="rowId">The row id.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public bool IsSelected(string rowId, string value)
        => Answer?.Rows is not null && Answer.Rows.TryGetValue(rowId, out var row) && row?.Contains(value, StringComparer.Ordinal) == true;

    /// <summary>
    /// Gets the first value of a row, or <see langword="null"/>.
    /// </summary>
    /// <param name="rowId">The row id.</param>
    /// <returns>The value.</returns>
    public string RowValue(string rowId)
        => Answer?.Rows is not null && Answer.Rows.TryGetValue(rowId, out var row) ? row?.FirstOrDefault() : null;

    /// <summary>
    /// Gets the answer value at a position, or <see langword="null"/>.
    /// </summary>
    /// <param name="index">The position.</param>
    /// <returns>The value.</returns>
    public string ValueAt(int index)
        => Answer?.Values is { } values && index >= 0 && index < values.Count ? values[index] : null;

    /// <summary>
    /// Gets the <c>aria-describedby</c> value: the hint and, when there are errors, the errors.
    /// </summary>
    /// <returns>The ids.</returns>
    public string DescribedBy()
        => HasErrors ? $"{HintId} {ErrorId}" : HintId;
}
