using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.AI.Services;

/// <summary>
/// The result of drafting a survey with AI.
/// </summary>
public sealed class SurveyDraftResult
{
    /// <summary>
    /// Gets or sets why no draft was made, or <see langword="null"/>.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets the new survey, a draft nobody can respond to yet.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the problems the publish checks found in the draft.
    /// </summary>
    public IReadOnlyList<SurveyValidationMessage> Messages { get; set; } = [];
}
