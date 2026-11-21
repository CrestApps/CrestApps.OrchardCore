using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Learning.Assessments.Handlers;

/// <summary>
/// When a published quiz closes on another date, moves the close date its attempts keep, so a review that waits for
/// the quiz to close opens on the new date, earlier or later. It runs after the request commits.
/// </summary>
internal sealed class QuizCloseDateHandler : ContentPartHandler<QuizPart>
{
    public override Task PublishedAsync(PublishContentContext context, QuizPart part)
    {
        var previous = context.PreviousItem?.Get<QuizPart>(nameof(QuizPart));

        if (previous is null || previous.ClosesUtc == part.ClosesUtc)
        {
            return Task.CompletedTask;
        }

        var quizId = context.ContentItem.ContentItemId;
        var previousClosesUtc = previous.ClosesUtc;
        var closesUtc = part.ClosesUtc;

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IAssessmentAttemptService>().MoveQuizCloseAsync(quizId, previousClosesUtc, closesUtc));

        return Task.CompletedTask;
    }
}
