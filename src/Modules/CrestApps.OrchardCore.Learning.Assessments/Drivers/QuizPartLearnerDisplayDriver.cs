using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

/// <summary>
/// The quiz panel on the quiz item: the rules, the attempts of the learner and the button to start or continue.
/// </summary>
internal sealed class QuizPartLearnerDisplayDriver : ContentPartDisplayDriver<QuizPart>
{
    private readonly AssessmentAttemptStore _attemptStore;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public QuizPartLearnerDisplayDriver(AssessmentAttemptStore attemptStore, IHttpContextAccessor httpContextAccessor)
    {
        _attemptStore = attemptStore;
        _httpContextAccessor = httpContextAccessor;
    }

    public override IDisplayResult Display(QuizPart part, BuildPartDisplayContext context)
        => Initialize<QuizPanelViewModel>("QuizPart_Learner", async model =>
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            model.QuizId = part.ContentItem.ContentItemId;
            model.Quiz = part;
            model.SignedIn = !string.IsNullOrEmpty(userId);
            model.QuestionCount = part.QuestionIds.Count + part.DrawRules.Sum(rule => rule.Count);

            if (model.SignedIn)
            {
                var attempts = await _attemptStore.ListAsync(userId, part.ContentItem.ContentItemId);

                model.Attempts = attempts.Reverse().ToList();
                model.Open = attempts.FirstOrDefault(attempt => attempt.IsOpen);

                var counted = attempts.Count(attempt => attempt.Status != AttemptStatus.Voided);
                model.AttemptsLeft = part.MaxAttempts is { } max ? Math.Max(max - counted, 0) : null;
            }
        }).Location("Detail", "Content:5");
}
