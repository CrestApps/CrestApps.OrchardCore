using System;
using System.Collections.Generic;
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
    private readonly ICallControlAuthorizationService _callControlAuthorizationService;
    private readonly ITransferDestinationResolver _transferDestinationResolver;
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
    /// <param name="callControlAuthorizationService">The shared call-control authorization boundary.</param>
    /// <param name="transferDestinationResolver">The typed transfer destination resolver.</param>
    public ContactCenterTransferService(
        IInteractionManager interactionManager,
        IActivityQueueService queueService,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IContactCenterEventPublisher publisher,
        ITelephonyCommandExecutor commandExecutor,
        IClock clock,
        ICallControlAuthorizationService callControlAuthorizationService = null,
        ITransferDestinationResolver transferDestinationResolver = null)
    {
        _interactionManager = interactionManager;
        _queueService = queueService;
        _voiceProviderResolver = voiceProviderResolver;
        _callControlAuthorizationService = callControlAuthorizationService;
        _transferDestinationResolver = transferDestinationResolver;
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

        if (_callControlAuthorizationService is not null && string.IsNullOrEmpty(request.InitiatedByUserId))
        {
            return TransferResult.Failure("The requested call is not available.");
        }

        var interaction = await _interactionManager.FindByIdAsync(request.InteractionId, cancellationToken);

        if (interaction is null)
        {
            return TransferResult.Failure("The interaction could not be found.");
        }

        var providerCallId = interaction.ProviderInteractionId;

        if (_callControlAuthorizationService is not null)
        {
            var authorization = await _callControlAuthorizationService.AuthorizeAsync(new CallControlAuthorizationContext
            {
                Principal = request.Principal,
                UserId = request.InitiatedByUserId,
                Verb = CallControlVerb.Transfer,
                InteractionId = interaction.ItemId,
                ProviderName = interaction.ProviderName,
            }, cancellationToken);

            if (!authorization.Succeeded)
            {
                return TransferResult.Failure(authorization.FailureReason);
            }

            providerCallId = authorization.ProviderCallId;
        }

        var destination = _transferDestinationResolver is null
            ? TransferDestinationResolutionResult.Success(request.TargetType, request.TargetId)
            : await _transferDestinationResolver.ResolveAsync(request, request.Principal, cancellationToken);

        if (!destination.Succeeded)
        {
            await PublishTransferDeniedAsync(request, interaction, destination.FailureReason, cancellationToken);

            return TransferResult.Failure(destination.FailureReason);
        }

        var provider = _voiceProviderResolver.Get(interaction.ProviderName);

        if (provider is not IContactCenterVoiceTransferProvider transferProvider ||
            !provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.CallTransfer) ||
            string.IsNullOrEmpty(providerCallId))
        {
            return TransferResult.Failure("The voice provider does not support call transfer.");
        }

        try
        {
            var providerResult = await _commandExecutor.ExecuteAsync(commandCancellationToken =>
                transferProvider.TransferAsync(new ContactCenterVoiceTransferRequest
                {
                    InteractionId = interaction.ItemId,
                    ProviderCallId = providerCallId,
                    TransferType = request.Type,
                    TargetType = destination.TargetType,
                    Target = destination.ResolvedTarget,
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
                ToParticipantId = destination.ResolvedTarget,
                TargetType = destination.TargetType.ToString(),
                RequestedUtc = now,
            };

            var reason = await ApplyTargetAsync(request, interaction, destination, CancellationToken.None);

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

    private async Task<string> ApplyTargetAsync(
        TransferRequest request,
        Interaction interaction,
        TransferDestinationResolutionResult destination,
        CancellationToken cancellationToken)
    {
        switch (destination.TargetType)
        {
            case InteractionTransferTargetType.Queue:
                if (!string.IsNullOrEmpty(interaction.ActivityItemId))
                {
                    await _queueService.EnqueueAsync(interaction.ActivityItemId, destination.ResolvedTarget, priority: null, cancellationToken);

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

    private Task PublishTransferDeniedAsync(
        TransferRequest request,
        Interaction interaction,
        string reason,
        CancellationToken cancellationToken)
    {
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.InteractionTransferDenied,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(Interaction),
            AggregateId = interaction.ItemId,
            ActorId = request.InitiatedByAgentId ?? interaction.AgentId,
            SourceComponent = ContactCenterConstants.Components.Interactions,
        };

        interactionEvent.SetData(new Dictionary<string, string>
        {
            ["targetType"] = request.TargetType.ToString(),
            ["reason"] = reason ?? string.Empty,
        });

        return _publisher.PublishAsync(interactionEvent, cancellationToken);
    }
}
