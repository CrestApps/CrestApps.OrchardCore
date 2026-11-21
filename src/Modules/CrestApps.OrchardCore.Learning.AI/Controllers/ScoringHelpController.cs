using CrestApps.OrchardCore.Learning.AI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.AI.Controllers;

/// <summary>
/// Scoring help for the coursework grading page. It returns a suggestion as JSON for the page to fill its fields with; it never
/// saves a grade.
/// </summary>
[Admin]
[Feature(LearningAIConstants.Features.Authoring)]
public sealed class ScoringHelpController : Controller
{
    private readonly EssayScoringAssistant _assistant;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScoringHelpController"/> class.
    /// </summary>
    /// <param name="assistant">The scoring help.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ScoringHelpController(EssayScoringAssistant assistant, IStringLocalizer<ScoringHelpController> stringLocalizer)
    {
        _assistant = assistant;
        S = stringLocalizer;
    }

    /// <summary>
    /// Suggests a score for a coursework submission.
    /// </summary>
    /// <param name="id">The submission.</param>
    /// <returns>The suggestion as JSON.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("learning/ai/scoring-help/submissions/{id}", "LearningAIScoringHelpSubmission")]
    public async Task<IActionResult> Submission(string id)
        => Write(await _assistant.SuggestForSubmissionAsync(User, id, HttpContext.RequestAborted));

    private IActionResult Write(EssayScoringResult result)
    {
        if (result.Error == EssayScoringResult.NotFound)
        {
            return NotFound();
        }

        if (result.Error is not null)
        {
            var message = result.Error switch
            {
                EssayScoringResult.NoModel => S["No AI model is set up for this site, so there is no suggestion. Grade the work yourself."],
                EssayScoringResult.NoText => S["This work has no text to read, so there is no suggestion. Grade the work yourself."],
                _ => S["The model's answer could not be used, so there is no suggestion. Try again, or grade the work yourself."],
            };

            return Json(new { error = result.Error, message = message.Value });
        }

        return Json(new
        {
            points = result.Suggestion.Points,
            choices = result.Suggestion.Choices,
            feedback = result.Suggestion.Feedback,
            message = S["A suggestion was filled in. Check it and change anything you disagree with; the grade is saved only when you save it."].Value,
        });
    }
}
