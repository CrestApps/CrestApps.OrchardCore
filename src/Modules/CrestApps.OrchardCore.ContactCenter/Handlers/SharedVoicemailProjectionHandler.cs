using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Files a voicemail in its queue's shared voicemail box once the call is sent to voicemail, when the call's line
/// delivers such messages to the queue's team rather than to one agent.
/// </summary>
/// <remarks>
/// Where the message goes is decided when the call is sent to voicemail, and recorded on the interaction; this only
/// files what was decided. Filing is keyed by the interaction, so a replayed event finds the message already filed.
/// The recording itself stays on the interaction and is played through recording governance, so a message filed
/// before its recording has finished ingesting becomes playable the moment it lands, with nothing more to update here.
/// </remarks>
public sealed class SharedVoicemailProjectionHandler : IContactCenterEventHandler
{
    private readonly IInteractionManager _interactionManager;
    private readonly IServiceProvider _serviceProvider;

    // The event publisher builds every event handler through its outbox, and filing a message records an event, so the
    // filing service and what it needs are resolved when an event is handled. Taken in the constructor they made a
    // dependency cycle through the publisher, and the tenant's services could not be built.

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedVoicemailProjectionHandler"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager used to read where the message was delivered.</param>
    /// <param name="serviceProvider">The scope's services, from which the filing service is resolved when needed.</param>
    public SharedVoicemailProjectionHandler(
        IInteractionManager interactionManager,
        IServiceProvider serviceProvider)
    {
        _interactionManager = interactionManager;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/SharedVoicemailProjection/v1";

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.NaturallyIdempotent;

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (interactionEvent.EventType != ContactCenterConstants.Events.CallSentToVoicemail)
        {
            return;
        }

        var interactionId = !string.IsNullOrEmpty(interactionEvent.InteractionId)
            ? interactionEvent.InteractionId
            : interactionEvent.AggregateId;

        if (string.IsNullOrEmpty(interactionId))
        {
            return;
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);
        var queueId = VoicemailDelivery.GetSharedQueueId(interaction);

        if (queueId is null)
        {
            return;
        }

        var voicemail = new SharedVoicemail
        {
            InteractionId = interaction.ItemId,
            QueueId = queueId,
            CallerNumber = interaction.CustomerAddress,
            ReceivedUtc = interactionEvent.OccurredUtc,
        };

        await ApplyContactAsync(voicemail, interaction.ActivityItemId, cancellationToken);
        await _serviceProvider.GetRequiredService<ISharedVoicemailService>().DeliverAsync(voicemail, cancellationToken);
    }

    // The team sees who called by name when inbound routing recognized the caller as a contact.
    private async Task ApplyContactAsync(SharedVoicemail voicemail, string activityItemId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(activityItemId))
        {
            return;
        }

        var activity = await _serviceProvider.GetRequiredService<IOmnichannelActivityManager>().FindByIdAsync(activityItemId, cancellationToken);

        if (string.IsNullOrEmpty(activity?.ContactContentItemId))
        {
            return;
        }

        voicemail.ContactContentItemId = activity.ContactContentItemId;
        voicemail.ContactContentType = activity.ContactContentType;

        var contact = await _serviceProvider.GetRequiredService<IContentManager>().GetAsync(activity.ContactContentItemId);

        if (!string.IsNullOrWhiteSpace(contact?.DisplayText))
        {
            voicemail.CallerName = contact.DisplayText;
        }
    }
}
