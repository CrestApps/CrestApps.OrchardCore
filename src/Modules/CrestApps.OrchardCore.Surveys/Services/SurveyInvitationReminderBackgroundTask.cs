using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Environment.Shell.Scope;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Sends the survey invitation reminders that are due, once an hour. Each invitation list is handled in its own child
/// shell scope, with a session of its own that commits on its own: a list that fails (an anonymous survey whose secret
/// can't be read on this server, or a save that fails) is logged and skipped, and never takes the other lists' updates
/// with it. Each list is its own batch of the daily e-mail limit, which it saves before the next list starts; once a
/// list finds the limit's lock busy, the rest of the run holds its e-mails instead of waiting for the lock per list.
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
        var logger = serviceProvider.GetRequiredService<ILogger<SurveyInvitationReminderBackgroundTask>>();
        var collectorIds = await session.QueryIndex<SurveyCollectorIndex>(index => index.Kind == SurveyCollectorKinds.Invitation && index.Status == nameof(CollectorStatus.Open), collection: SurveysConstants.CollectionName)
            .ListAsync(cancellationToken);

        // Ends this scope's read transaction before the lists' own scopes write.
        await session.SaveChangesAsync(cancellationToken);

        // Once one list couldn't get the daily e-mail limit's lock (another send holds it), the later lists of the run hold
        // their e-mails without waiting for it again; their notifications to users still go out.
        var emailsHeld = false;

        foreach (var row in collectorIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ShellScope.UsingChildScopeAsync(async scope =>
                {
                    var emailLimit = scope.ServiceProvider.GetRequiredService<SurveyInvitationEmailLimit>();

                    if (emailsHeld)
                    {
                        emailLimit.HoldEmails();
                    }

                    try
                    {
                        // The list's send commits as it goes and, once cancelled, stops cleanly between commits (R8-M3);
                        // whatever it left is saved without the token, and the next list isn't started.
                        await scope.ServiceProvider.GetRequiredService<SurveyInvitationService>().RemindAsync(row.ItemId, force: false, cancellationToken);
                        await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(CancellationToken.None);
                    }
                    finally
                    {
                        emailsHeld |= emailLimit.LockUnavailable;
                    }
                });
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
