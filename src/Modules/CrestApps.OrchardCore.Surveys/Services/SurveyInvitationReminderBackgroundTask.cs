using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Sends the survey invitation reminders that are due, once an hour.
/// </summary>
[BackgroundTask(
    Title = "Survey invitation reminders",
    Schedule = "41 * * * *",
    Description = "Reminds people who were invited to a survey and have not completed it.",
    LockTimeout = 1_000,
    LockExpiration = 900_000)]
public sealed class SurveyInvitationReminderBackgroundTask : IBackgroundTask
{
    /// <inheritdoc />
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var session = serviceProvider.GetRequiredService<ISession>();
        var invitations = serviceProvider.GetRequiredService<SurveyInvitationService>();
        var collectorIds = await session.QueryIndex<SurveyCollectorIndex>(index => index.Kind == SurveyCollectorKinds.Invitation && index.Status == nameof(CollectorStatus.Open), collection: SurveysConstants.CollectionName)
            .ListAsync(cancellationToken);

        foreach (var row in collectorIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await invitations.RemindAsync(row.ItemId, force: false, cancellationToken);
        }
    }
}
