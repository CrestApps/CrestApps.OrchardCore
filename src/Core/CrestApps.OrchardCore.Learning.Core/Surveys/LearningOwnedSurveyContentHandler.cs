using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// Removes the survey a course item owns when the item is removed: deleted when nobody responded, archived otherwise so
/// its responses and results stay.
/// </summary>
/// <remarks>
/// In a shell the survey is removed in a deferred task, after the request that removed the item committed. Deleting a
/// survey purges its records a batch at a time, each committed in a scope of its own; run inside the request, those
/// batches would wait on the request's own pending writes (SQLite allows one writer, so they stalled until the busy
/// timeout and the survey stayed). The task first checks the item is still gone, so a request that failed to commit
/// never removes the survey of an item it didn't remove.
/// </remarks>
public sealed class LearningOwnedSurveyContentHandler : ContentHandlerBase
{
    private readonly ISurveyManager _surveyManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningOwnedSurveyContentHandler"/> class.
    /// </summary>
    /// <param name="surveyManager">The survey manager.</param>
    public LearningOwnedSurveyContentHandler(ISurveyManager surveyManager)
    {
        _surveyManager = surveyManager;
    }

    /// <inheritdoc />
    public override Task RemovedAsync(RemoveContentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Removing one version of a draftable item leaves the item: only removing all of it removes its survey.
        if (!context.NoActiveVersionLeft)
        {
            return Task.CompletedTask;
        }

        var contentItemId = context.ContentItem.ContentItemId;

        if (ShellScope.Current is null)
        {
            // Outside a shell (tools and tests) there is no request to wait for.
            return RemoveSurveysAsync(_surveyManager, contentItemId);
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;

            // The request committed when this runs; if it didn't, the item is still there and keeps its survey.
            if (await services.GetRequiredService<IContentManager>().GetAsync(contentItemId, VersionOptions.Latest) is not null)
            {
                return;
            }

            try
            {
                await RemoveSurveysAsync(services.GetRequiredService<ISurveyManager>(), contentItemId);
            }
            catch (Exception exception)
            {
                services.GetRequiredService<ILogger<LearningOwnedSurveyContentHandler>>().LogError(
                    exception,
                    "The survey of the removed course item '{ContentItemId}' could not be removed. Delete or archive it from Surveys.",
                    contentItemId);
            }
        });

        return Task.CompletedTask;
    }

    internal static async Task RemoveSurveysAsync(ISurveyManager surveyManager, string contentItemId)
    {
        foreach (var survey in await surveyManager.ListByOwnerAsync(LearningSurveyContexts.Owner(contentItemId)))
        {
            if (await surveyManager.DeleteSurveyAsync(survey.ItemId) == SurveyErrorCode.None)
            {
                continue;
            }

            if (survey.Status == SurveyStatus.Open)
            {
                await surveyManager.ChangeStatusAsync(survey.ItemId, SurveyStatus.Closed);
            }

            if (survey.Status != SurveyStatus.Draft)
            {
                await surveyManager.ChangeStatusAsync(survey.ItemId, SurveyStatus.Archived);
            }
        }
    }
}
