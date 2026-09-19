using CrestApps.Core.Omnichannel.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Omnichannel.Sms.BackgroundTasks;

/// <summary>
/// Recovers automated SMS conversations whose reply was owed but never sent.
/// </summary>
/// <remarks>
/// The inbound webhook acknowledges Twilio immediately and generates the reply on a background scope, and the
/// single-active-generation registry that serializes those replies lives in memory. Without a distributed backing
/// (for example Redis) that registry is per-node and does not survive a restart, so a reply that was mid-flight when
/// the process stopped is simply lost: the customer's message sits in the transcript unanswered while the activity
/// waits in <see cref="ActivityStatus.AwaitingCustomerAnswer"/>. The no-response timeout would eventually mark such a
/// conversation <see cref="ActivityStatus.Failed"/> as if the customer went quiet, which is wrong — it was our reply
/// that was dropped. This task periodically finds those conversations (a trailing customer message with no reply after
/// it) and re-drives the handler, which is idempotent: the message is not stored twice, and if a live generation has
/// meanwhile answered, the owed-reply gate simply sends nothing.
/// </remarks>
[BackgroundTask(
    Title = "Automated SMS Owed-Reply Recovery",
    Schedule = "*/10 * * * *",
    Description = "Re-drives automated SMS conversations whose AI reply was lost before it was sent.",
    LockTimeout = 5_000,
    LockExpiration = _leaseMilliseconds)]
public sealed class SmsOwedReplyRecoveryBackgroundTask : IBackgroundTask
{
    private const int _leaseMilliseconds = 300_000;
    private const int _batchSize = 100;
    private const int _maxConversationsPerInvocation = 200;

    // A reply is only recovered when the customer's unanswered message is recent. This is the whole point of the
    // staleness bound: recovery exists to answer a reply that was mid-flight when the process stopped (picked up on
    // the next scheduled run, minutes later), NOT to resurrect a thread the customer sent to hours or days ago. Waking
    // an old, wound-down conversation with a late reply is worse than staying silent — the customer has moved on, and
    // the no-response timeout already governs those. The window comfortably covers a normal restart and a couple of
    // missed runs, while excluding anything genuinely stale.
    private const int _maxOwedReplyAgeMinutes = 30;

    /// <summary>
    /// Asynchronously performs the do work operation.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<ISmsOwedReplyRecoveryCycle>().RunAsync(cancellationToken);
}
