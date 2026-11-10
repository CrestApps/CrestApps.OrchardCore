using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Telemetry;
using OrchardCore.Infrastructure;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Services;

/// <summary>
/// Sends the texts the automated SMS conversations write — the opening message, every reply and every re-engagement
/// nudge — through the provider that owns the sending number, and measures each one as a send on
/// <see cref="MessagingTelemetry.SendDuration"/>, as the messaging workspace's own sends are.
/// </summary>
internal static class SmsAutomatedSend
{
    /// <summary>
    /// Sends a text through the provider that owns its sending number.
    /// </summary>
    /// <param name="router">The router that picks the provider owning the sending number.</param>
    /// <param name="message">The text to send.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider's result.</returns>
    public static async Task<Result> SendAsync(ISmsProviderRouter router, SmsMessage message, CancellationToken cancellationToken)
    {
        using var telemetry = MessagingTelemetry.StartSend(OmnichannelConstants.Channels.Sms);

        try
        {
            var result = await router.SendAsync(message, cancellationToken);

            if (result.Succeeded)
            {
                telemetry.Complete(MessagingTelemetry.SendResults.Sent);
            }
            else
            {
                telemetry.Complete(MessagingTelemetry.SendResults.Failed, CrestAppsOrchardCoreTelemetry.OutcomeError);
            }

            return result;
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);

            throw;
        }
    }
}
