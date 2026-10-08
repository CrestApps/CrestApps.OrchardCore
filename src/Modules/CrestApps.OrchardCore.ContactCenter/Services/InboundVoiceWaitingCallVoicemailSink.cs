using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// The voice feature's answer to a queue that wants a waiting caller in voicemail: the inbound processor already
/// knows how to take a waiting call out of its queue and end it through the provider's voicemail path, so this
/// hands the item to it.
/// </summary>
public sealed class InboundVoiceWaitingCallVoicemailSink : IWaitingCallVoicemailSink
{
    /// <remarks>
    /// Deferred because the dependency genuinely goes both ways: admitting a call asks the queue-limit service
    /// whether the queue is full, the limit service sends an over-limit caller here, and here is the processor
    /// again. A <see cref="Lazy{T}"/> declares the dependency and resolves it only when a caller actually
    /// overflows, which is the one moment the processor is not already on the stack.
    /// </remarks>
    private readonly Lazy<IInboundVoiceCallProcessor> _inboundProcessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="InboundVoiceWaitingCallVoicemailSink"/> class.
    /// </summary>
    /// <param name="inboundProcessor">The inbound processor that owns the voicemail path.</param>
    public InboundVoiceWaitingCallVoicemailSink(Lazy<IInboundVoiceCallProcessor> inboundProcessor)
    {
        _inboundProcessor = inboundProcessor;
    }

    /// <inheritdoc/>
    public Task<bool> SendToVoicemailAsync(QueueItem item, string reasonCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrEmpty(reasonCode);

        if (string.IsNullOrEmpty(item.ActivityItemId))
        {
            return Task.FromResult(false);
        }

        return _inboundProcessor.Value.SendWaitingToVoicemailAsync(item.ActivityItemId, reasonCode, cancellationToken);
    }
}
