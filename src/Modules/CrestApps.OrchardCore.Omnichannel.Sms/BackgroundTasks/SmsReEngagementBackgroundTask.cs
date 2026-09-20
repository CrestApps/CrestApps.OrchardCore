using CrestApps.Core.Omnichannel.Sms.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Omnichannel.Sms.BackgroundTasks;

/// <summary>
/// Proactively re-engages automated SMS contacts who have gone quiet, when the loading campaign enabled it.
/// </summary>
/// <remarks>
/// When a contact does not reply for longer than the configured silence window, this task sends an AI-composed
/// follow-up to invite a response, up to the configured maximum. Every send here is background-initiated, so it is
/// gated by the campaign's business-hours calendar evaluated in the contact's local time zone — we never nudge a
/// contact after hours. (A live reply to a contact who is actively messaging goes through the inbound webhook, not
/// this task, and is never gated.) The per-conversation lock and the single-active-generation registry are honored so
/// a nudge can never collide with a live reply.
/// </remarks>
[BackgroundTask(
    Title = "Automated SMS Re-Engagement",
    Schedule = "*/5 * * * *",
    Description = "Sends follow-up messages to automated SMS contacts who have gone quiet, within business hours.",
    LockTimeout = 5_000,
    LockExpiration = _leaseMilliseconds)]
public sealed class SmsReEngagementBackgroundTask : IBackgroundTask
{
    private const int _leaseMilliseconds = 300_000;
    private const int _batchSize = 100;
    private const int _maxConversationsPerInvocation = 200;

    private const string ReEngagementSystemPromptPrefix =
        """
        You are the sales agent in an ongoing SMS conversation with a customer who has not replied to your last message.
        Write a brief, friendly follow-up that re-engages them and invites a response. Follow this guidance from the
        campaign:
        """;

    private const string ReEngagementSystemPromptSuffix =
        """
        Keep it short (one or two sentences), natural, and do not repeat your previous message word for word. Reply with
        only the message text to send — no preamble, quotes, or labels.
        """;

    private static string BuildReEngagementSystemMessage(string guidance)
        => string.IsNullOrWhiteSpace(guidance)
            ? $"{ReEngagementSystemPromptPrefix}\n{ReEngagementSystemPromptSuffix}"
            : $"{ReEngagementSystemPromptPrefix} {guidance.Trim()}\n{ReEngagementSystemPromptSuffix}";

    /// <summary>
    /// Asynchronously performs the do work operation.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<ISmsReEngagementCycle>().RunAsync(cancellationToken);
}
