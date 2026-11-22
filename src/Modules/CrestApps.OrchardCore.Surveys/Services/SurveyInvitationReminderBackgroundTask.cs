using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Sends the survey invitation reminders that are due, once an hour. One invitation list that fails (an anonymous
/// survey whose secret can't be read on this server, for example) is logged and skipped, so the lists after it still
/// get their reminders.
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
        var logger = serviceProvider.GetRequiredService<ILogger<SurveyInvitationReminderBackgroundTask>>();
        var collectorIds = await session.QueryIndex<SurveyCollectorIndex>(index => index.Kind == SurveyCollectorKinds.Invitation && index.Status == nameof(CollectorStatus.Open), collection: SurveysConstants.CollectionName)
            .ListAsync(cancellationToken);

        foreach (var row in collectorIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await invitations.RemindAsync(row.ItemId, force: false, cancellationToken);
            }
            catch (SurveyAnonymitySecretUnreadableException exception)
            {
                logger.LogWarning(exception, "The reminders of the invitation list '{CollectorId}' were skipped: the anonymity secret of its survey can't be read on this server.", row.ItemId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "The reminders of the invitation list '{CollectorId}' failed.", row.ItemId);
            }
        }
    }
}
