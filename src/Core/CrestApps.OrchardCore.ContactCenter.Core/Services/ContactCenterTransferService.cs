using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterTransferService"/>.
/// </summary>
public sealed class ContactCenterTransferService : IContactCenterTransferService
{
    private readonly IInteractionManager _interactionManager;
    private readonly IActivityQueueService _queueService;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly ITelephonyCommandExecutor _commandExecutor;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterTransferService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="queueService">The queue service used to re-enqueue queue transfers.</param>
    /// <param name="voiceProviderResolver">The voice provider resolver.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="commandExecutor">The executor that provides a bounded server-owned provider-operation token.</param>
    /// <param name="clock">The clock used to stamp transfer times.</param>
    public ContactCenterTransferService(
        IInteractionManager interactionManager,
        IActivityQueueService queueService,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IContactCenterEventPublisher publisher,
        ITelephonyCommandExecutor commandExecutor,
        IClock clock)
    {
        _interactionManager = interactionManager;
        _queueService = queueService;
        _voiceProviderResolver = voiceProviderResolver;
        _publisher = publisher;
        _commandExecutor = commandExecutor;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<TransferResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.InteractionId))
        {
            return TransferResult.Failure("A transfer requires an interaction.");
        }

        if (string.IsNullOrEmpty(request.TargetId))
        {
            return TransferResult.Failure("A transfer requires a destination.");
        }

        var interaction = await _interactionManager.FindByIdAsync(request.InteractionId, cancellationToken);

        if (interaction is null)
        {
            return TransferResult.Failure("The interaction could not be found.");
        }

        var provider = _voiceProviderResolver.Get(interaction.ProviderName);

        if (provider is not IContactCenterVoiceTransferProvider transferProvider ||
            !provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.CallTransfer) ||
            string.IsNullOrEmpty(interaction.ProviderInteractionId))
        {
            return TransferResult.Failure("The voice provider does not support call transfer.");
        }

        try
        {
            var providerResult = await _commandExecutor.ExecuteAsync(commandCancellationToken =>
                transferProvider.TransferAsync(new ContactCenterVoiceTransferRequest
                {
                    InteractionId = interaction.ItemId,
                    ProviderCallId = interaction.ProviderInteractionId,
                    TransferType = request.Type,
                    TargetType = request.TargetType,
                    Target = request.TargetId,
                }, commandCancellationToken));

            if (providerResult?.Succeeded != true || providerResult.OutcomeUnknown)
            {
                return TransferResult.Failure(
                    providerResult?.ErrorMessage ?? "The voice provider did not confirm the call transfer.");
            }

            var now = _clock.UtcNow;
            var entry = new InteractionTransferHistoryEntry
            {
                FromParticipantId = request.InitiatedByAgentId ?? interaction.AgentId,
                ToParticipantId = request.TargetId,
                TargetType = request.TargetType.ToString(),
                RequestedUtc = now,
            };

            var reason = await ApplyTargetAsync(request, interaction, CancellationToken.None);

            entry.CompletedUtc = now;
            entry.Result = reason;
            interaction.TransferHistory.Add(entry);
            interaction.Status = InteractionStatus.Transferring;

            await _interactionManager.UpdateAsync(interaction, cancellationToken: CancellationToken.None);

            var interactionEvent = new InteractionEvent
            {
                EventType = ContactCenterConstants.Events.InteractionTransferred,
                InteractionId = interaction.ItemId,
                AggregateType = nameof(Interaction),
                AggregateId = interaction.ItemId,
                ActorId = request.InitiatedByAgentId ?? interaction.AgentId,
                SourceComponent = ContactCenterConstants.Components.Interactions,
            };

            await _publisher.PublishAsync(interactionEvent, CancellationToken.None);

            return TransferResult.Success(reason);
        }
        catch (TimeoutException)
        {
            return TransferResult.Unknown(
                "The voice provider did not confirm the call transfer before the server timeout; the provider outcome is unknown.");
        }
        catch (OperationCanceledException)
        {
            return TransferResult.Unknown(
                "The call transfer was interrupted before the provider outcome could be confirmed.");
        }
    }

    private async Task<string> ApplyTargetAsync(TransferRequest request, Interaction interaction, CancellationToken cancellationToken)
    {
        switch (request.TargetType)
        {
            case InteractionTransferTargetType.Queue:
                if (!string.IsNullOrEmpty(interaction.ActivityItemId))
                {
                    await _queueService.EnqueueAsync(interaction.ActivityItemId, request.TargetId, priority: null, cancellationToken);

                    return "Re-queued to the target queue.";
                }

                return "Queued transfer requested without an activity.";
            case InteractionTransferTargetType.Agent:
                return "Transfer to agent requested.";
            case InteractionTransferTargetType.External:
                return "Transfer to external destination requested.";
            case InteractionTransferTargetType.EntryPoint:
                return "Transfer to entry point requested.";
            default:
                return "Transfer requested.";
        }
    }
}
