using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Voice.BackgroundTasks;

/// <summary>
/// Concludes automated voice calls whose end the provider never reported.
/// </summary>
/// <remarks>
/// The Contact Center's orphaned-activity recovery deliberately leaves automated calls alone -- they carry none of
/// the reservations and interactions it reads -- so a lost hangup left an automated call in progress for good, and
/// the lead behind it was skipped by every later load as already having an open activity. See
/// <see cref="StrandedVoiceCallPolicy"/> for when a call is taken as over.
/// </remarks>
[BackgroundTask(
    Title = "Stranded Automated Voice Call Recovery",
    Schedule = "*/5 * * * *",
    Description = "Concludes automated voice calls whose end the telephony provider never reported.",
    LockTimeout = 5_000,
    LockExpiration = 120_000)]
public sealed class StrandedVoiceCallRecoveryBackgroundTask : IBackgroundTask
{
    private const int MaxCallsPerRun = 50;

    private static readonly ActivityStatus[] _openCallStatuses =
    [
        ActivityStatus.AwaitingCustomerAnswer,
        ActivityStatus.InProgress,
    ];

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(run => RecoverAsync(serviceProvider, run, cancellationToken));

    private static async Task RecoverAsync(IServiceProvider serviceProvider, MeasuredTaskRun run, CancellationToken cancellationToken)
    {
        var session = serviceProvider.GetRequiredService<ISession>();
        var promptStore = serviceProvider.GetRequiredService<IAIChatSessionPromptStore>();
        var loop = serviceProvider.GetRequiredService<IVoiceAgentConversationLoop>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var logger = serviceProvider.GetRequiredService<ILogger<StrandedVoiceCallRecoveryBackgroundTask>>();

        var now = clock.UtcNow;
        var phone = OmnichannelConstants.Channels.Phone;

        // A call handed to a person is the Contact Center's from then on, and is left out entirely.
        var candidates = await session
            .Query<OmnichannelActivity, OmnichannelActivityIndex>(
                index => index.Status.IsIn(_openCallStatuses) &&
                    index.InteractionType == ActivityInteractionType.Automated &&
                    index.Channel == phone &&
                    !index.AiEscalated,
                collection: OmnichannelConstants.CollectionName)
            .OrderBy(index => index.Id)
            .Take(MaxCallsPerRun)
            .ListAsync(cancellationToken);

        var concluded = 0;
        var failed = 0;

        // A full page means there may be more stranded calls than one run takes on.
        if (candidates.Count >= MaxCallsPerRun)
        {
            run.BudgetExceeded();
        }

        foreach (var activity in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                DateTime? dialedUtc = activity.TryGet<AutomatedVoiceCallDial>(out var dial) ? dial.DialedUtc : null;
                DateTime? lastTurnUtc = null;

                if (!string.IsNullOrWhiteSpace(activity.AISessionId))
                {
                    var prompts = await promptStore.GetPromptsAsync(activity.AISessionId);

                    if (prompts is { Count: > 0 })
                    {
                        lastTurnUtc = prompts.Max(prompt => prompt.CreatedUtc);
                    }
                }

                if (!StrandedVoiceCallPolicy.IsStranded(now, dialedUtc, lastTurnUtc, activity.CreatedUtc, activity.ScheduledUtc))
                {
                    continue;
                }

                if (await loop.ConcludeStrandedCallAsync(activity.ItemId, cancellationToken))
                {
                    concluded++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                logger.LogError(ex, "Failed to recover stranded AI voice activity '{ActivityId}'.", activity.ItemId.SanitizeLogValue());
            }
        }

        run.AddItems(concluded, "concluded");
        run.AddItems(failed, "failed");

        if ((concluded > 0 || failed > 0) && logger.IsEnabled(LogLevel.Information))
        {
            var candidateCount = candidates.Count;

            VoiceLog.StrandedRecoveryRun(logger, candidateCount, concluded, failed);
        }
    }
}
