using System.Diagnostics;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Telemetry;

/// <summary>
/// The measurements of outbound messages: the sends handed to a provider, the outbox that retries them, and the
/// opt-outs and opt-ins recorded on a channel.
/// </summary>
public static partial class MessagingTelemetry
{
    /// <summary>
    /// Starts measuring an outbound message handed to a provider: the <c>crestapps.messaging.send</c> span and
    /// <see cref="SendDuration"/>, tagged with the result. Add the provider with <see cref="SetProvider(MessagingMeasurement, string)"/>.
    /// </summary>
    /// <param name="channel">The technical name of the channel, such as <see cref="OmnichannelConstants.Channels.Sms"/>.</param>
    /// <returns>The measurement.</returns>
    public static MessagingMeasurement StartSend(string channel)
    {
        return Start("crestapps.messaging.send", SendDuration, channel, ActivityKind.Internal);
    }

    /// <summary>
    /// Adds the provider that sends a message to its measurement.
    /// </summary>
    /// <param name="measurement">The send measurement.</param>
    /// <param name="provider">The technical name of the provider, or <see langword="null"/> when none resolved.</param>
    public static void SetProvider(MessagingMeasurement measurement, string provider)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        measurement.AddTag(CrestAppsOrchardCoreTelemetry.ProviderTag, GetProviderTag(provider));
    }

    /// <summary>
    /// Records how a send ended, from the provider's dispatch result.
    /// </summary>
    /// <param name="measurement">The send measurement.</param>
    /// <param name="dispatch">The provider's dispatch result.</param>
    public static void CompleteSend(MessagingMeasurement measurement, MessageDispatchResult dispatch)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        ArgumentNullException.ThrowIfNull(dispatch);

        var result = GetSendResult(dispatch);

        measurement.Complete(
            result,
            result == SendResults.Sent
                ? CrestAppsOrchardCoreTelemetry.OutcomeOk
                : CrestAppsOrchardCoreTelemetry.OutcomeError);
    }

    /// <summary>
    /// Gets the <see cref="SendDuration"/> result of a provider's dispatch result.
    /// </summary>
    /// <param name="dispatch">The dispatch result.</param>
    /// <returns>The bounded result.</returns>
    public static string GetSendResult(MessageDispatchResult dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);

        if (dispatch.Succeeded)
        {
            return SendResults.Sent;
        }

        return dispatch.ErrorCode switch
        {
            OmnichannelConstants.SmsErrorCodes.RecipientOptedOut => SendResults.OptedOut,
            OmnichannelConstants.SmsErrorCodes.MediaNotSupported => SendResults.MediaNotSupported,
            _ => SendResults.Failed,
        };
    }

    /// <summary>
    /// Records one re-attempt the outbox made.
    /// </summary>
    /// <param name="channel">The technical name of the channel.</param>
    /// <param name="result">The result, one of <see cref="OutboxResults"/>.</param>
    public static void RecordOutboxRetry(string channel, string result)
    {
        if (!OutboxRetry.Enabled)
        {
            return;
        }

        Add(OutboxRetry, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannelTag(channel) },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        });
    }

    /// <summary>
    /// Gets why an outbound message was given up on.
    /// </summary>
    /// <param name="attempts">The number of attempts made.</param>
    /// <returns><see cref="OutboxResults.RetriesExhausted"/> when its last scheduled attempt was made, otherwise
    /// <see cref="OutboxResults.NotRetryable"/>.</returns>
    public static string GetOutboxFailureReason(int attempts)
    {
        return attempts >= OutboundDeliveryState.MaxAttempts
            ? OutboxResults.RetriesExhausted
            : OutboxResults.NotRetryable;
    }

    /// <summary>
    /// Records an opt-out or an opt-in.
    /// </summary>
    /// <param name="channel">The technical name of the channel.</param>
    /// <param name="source">Where it came from, one of <see cref="OptOutSources"/>.</param>
    /// <param name="optedOut"><see langword="true"/> for an opt-out, <see langword="false"/> for an opt-in.</param>
    public static void RecordOptOut(string channel, string source, bool optedOut)
    {
        if (!OptOut.Enabled)
        {
            return;
        }

        Add(OptOut, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannelTag(channel) },
            { SourceTag, source ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { ActionTag, optedOut ? "opt_out" : "opt_in" },
        });
    }

    /// <summary>
    /// The results of <see cref="SendDuration"/>.
    /// </summary>
    public static class SendResults
    {
        /// <summary>
        /// The provider accepted the message.
        /// </summary>
        public const string Sent = "sent";

        /// <summary>
        /// The provider refused the message, or could not be reached.
        /// </summary>
        public const string Failed = "failed";

        /// <summary>
        /// The provider refused the message because the recipient opted out with it or the carrier.
        /// </summary>
        public const string OptedOut = "opted_out";

        /// <summary>
        /// The message carried pictures the provider cannot send.
        /// </summary>
        public const string MediaNotSupported = "media_not_supported";

        /// <summary>
        /// No enabled provider serves the sending address.
        /// </summary>
        public const string NoProvider = "no_provider";
    }

    /// <summary>
    /// The results of <see cref="OutboxRetry"/> and the reasons logged when a message is given up on.
    /// </summary>
    public static class OutboxResults
    {
        /// <summary>
        /// The re-attempt was accepted by the provider.
        /// </summary>
        public const string Sent = "sent";

        /// <summary>
        /// The re-attempt was refused and the message is queued for another one.
        /// </summary>
        public const string Rescheduled = "rescheduled";

        /// <summary>
        /// The re-attempt was refused and the message is given up on.
        /// </summary>
        public const string Failed = "failed";

        /// <summary>
        /// The message was given up on after its last scheduled attempt.
        /// </summary>
        public const string RetriesExhausted = "retries_exhausted";

        /// <summary>
        /// The message was given up on because the provider's refusal is never retried.
        /// </summary>
        public const string NotRetryable = "not_retryable";
    }

    /// <summary>
    /// The values of the <see cref="SourceTag"/> of <see cref="OptOut"/>.
    /// </summary>
    public static class OptOutSources
    {
        /// <summary>
        /// The contact texted an opt-out or opt-in keyword, such as STOP or START.
        /// </summary>
        public const string Keyword = "keyword";

        /// <summary>
        /// The provider refused a send because the recipient opted out with it or the carrier.
        /// </summary>
        public const string ProviderRefusal = "provider_refusal";

        /// <summary>
        /// The contact asked an automated (AI) conversation to stop.
        /// </summary>
        public const string AutomatedConversation = "automated_conversation";
    }
}
