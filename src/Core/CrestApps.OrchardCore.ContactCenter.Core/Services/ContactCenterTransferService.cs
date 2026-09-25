using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using ProviderVoiceEvent = CrestApps.OrchardCore.Telephony.Models.ProviderVoiceEvent;
using VoiceCallState = CrestApps.OrchardCore.Telephony.Models.VoiceCallState;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterTransferService"/>.
/// </summary>
/// <remarks>
/// Where the call goes decides who moves it. An agent or a queue is a Contact Center destination the provider cannot
/// dial, so the Contact Center routes the call itself and the provider only holds the caller and drops the old agent.
/// An external number is somewhere the provider can reach, so the provider moves the call and the Contact Center
/// records that it left.
/// </remarks>
public sealed class ContactCenterTransferService : IContactCenterTransferService
{
    private readonly IInteractionManager _interactionManager;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly ICallControlAuthorizationService _callControlAuthorizationService;
    private readonly ITransferDestinationResolver _transferDestinationResolver;
    private readonly ITransferredCallRouter _router;
    private readonly ITransferAgentReleaseService _agentRelease;
    private readonly IProviderVoiceEventService _providerVoiceEventService;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly ITelephonyCommandExecutor _commandExecutor;
    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterTransferService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="voiceProviderResolver">The voice provider resolver.</param>
    /// <param name="callControlAuthorizationService">The shared call-control authorization boundary.</param>
    /// <param name="transferDestinationResolver">The typed transfer destination resolver.</param>
    /// <param name="router">The router that offers a call transferred to an agent or a queue.</param>
    /// <param name="agentRelease">The service that takes the transferring agent off the call.</param>
    /// <param name="providerVoiceEventService">The provider-truth ingestion that settles a call transferred out.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="commandExecutor">The executor that provides a bounded server-owned provider-operation token.</param>
    /// <param name="session">The unit of work.</param>
    /// <param name="clock">The clock used to stamp transfer times.</param>
    public ContactCenterTransferService(
        IInteractionManager interactionManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        ICallControlAuthorizationService callControlAuthorizationService,
        ITransferDestinationResolver transferDestinationResolver,
        ITransferredCallRouter router,
        ITransferAgentReleaseService agentRelease,
        IProviderVoiceEventService providerVoiceEventService,
        IContactCenterEventPublisher publisher,
        ITelephonyCommandExecutor commandExecutor,
        ISession session,
        IClock clock)
    {
        _interactionManager = interactionManager;
        _voiceProviderResolver = voiceProviderResolver;
        _callControlAuthorizationService = callControlAuthorizationService;
        _transferDestinationResolver = transferDestinationResolver;
        _router = router;
        _agentRelease = agentRelease;
        _providerVoiceEventService = providerVoiceEventService;
        _publisher = publisher;
        _commandExecutor = commandExecutor;
        _session = session;
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

        if (string.IsNullOrEmpty(request.InitiatedByUserId))
        {
            return TransferResult.Failure("The requested call is not available.");
        }

        // A warm transfer is three steps the agent drives -- consult, then complete or cancel -- and each has its own
        // command. Treating one here as a blind transfer is what used to drop callers on people who had not agreed.
        if (request.Type != InteractionTransferType.Blind)
        {
            return TransferResult.Failure("A warm transfer starts with a consult; start the consult, then complete or cancel it.");
        }

        var interaction = await _interactionManager.FindByIdAsync(request.InteractionId, cancellationToken);

        if (interaction is null)
        {
            return TransferResult.Failure("The interaction could not be found.");
        }

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

        var destination = await _transferDestinationResolver.ResolveAsync(request, request.Principal, cancellationToken);

        if (!destination.Succeeded)
        {
            await _publisher.PublishAsync(
                TransferEventFactory.Denied(interaction, authorization.AgentId, request.InitiatedByUserId, request.TargetType, destination.FailureReason, _clock.UtcNow),
                cancellationToken);

            return TransferResult.Failure(destination.FailureReason);
        }

        var context = new TransferRoutingContext
        {
            Interaction = interaction,
            Session = authorization.CallSession,
            TransferringAgentId = authorization.AgentId ?? request.InitiatedByAgentId ?? interaction.AgentId,
            TransferringUserId = request.InitiatedByUserId,
            TargetId = destination.ResolvedTarget,
        };

        // The caller keeps talking to the agent until the move is under way, so the provider-facing work below runs on
        // a server-owned token: an agent closing the phone mid-request must not leave the call half moved.
        return destination.TargetType switch
        {
            InteractionTransferTargetType.Agent => await _router.RouteToAgentAsync(context, CancellationToken.None),
            InteractionTransferTargetType.Queue => await _router.RouteToQueueAsync(context, CancellationToken.None),
            InteractionTransferTargetType.External => await TransferExternallyAsync(context, authorization.ProviderCallId, CancellationToken.None),
            _ => TransferResult.Failure("Calls can be transferred to an agent, a queue, or an external number."),
        };
    }

    private async Task<TransferResult> TransferExternallyAsync(TransferRoutingContext context, string providerCallId, CancellationToken cancellationToken)
    {
        var interaction = context.Interaction;
        var provider = _voiceProviderResolver.Get(interaction.ProviderName);

        if (provider is not IContactCenterVoiceTransferProvider transferProvider ||
            !provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.CallTransfer) ||
            string.IsNullOrEmpty(providerCallId))
        {
            return TransferResult.Failure("The voice provider does not support call transfer.");
        }

        ContactCenterVoiceProviderResult providerResult;

        try
        {
            providerResult = await _commandExecutor.ExecuteAsync(commandCancellationToken =>
                transferProvider.TransferAsync(new ContactCenterVoiceTransferRequest
                {
                    InteractionId = interaction.ItemId,
                    ProviderCallId = providerCallId,
                    TransferType = InteractionTransferType.Blind,
                    TargetType = InteractionTransferTargetType.External,
                    Target = context.TargetId,
                }, commandCancellationToken));
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

        if (providerResult?.Succeeded != true || providerResult.OutcomeUnknown)
        {
            return TransferResult.Failure(
                providerResult?.ErrorMessage ?? "The voice provider did not confirm the call transfer.");
        }

        var now = _clock.UtcNow;
        var entry = InteractionTransferHistory.Open(interaction, context.TransferringAgentId, InteractionTransferTargetType.External, context.TargetId, now, InteractionTransferHistory.SentToExternalNumber);
        entry.CompletedUtc = now;

        // The agent's legs are read before the call settles, because settling ends every leg on the topology.
        var agentLegs = context.Session?.Legs
            .Where(leg =>
                leg is not null &&
                leg.Role == CallPartyRole.Agent &&
                !leg.EndedUtc.HasValue &&
                !string.IsNullOrWhiteSpace(leg.ProviderLegId) &&
                !string.Equals(leg.ProviderLegId, context.Session.ProviderCallId, StringComparison.Ordinal))
            .Select(leg => leg.ProviderLegId)
            .ToArray() ?? [];

        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        await _publisher.PublishAsync(
            TransferEventFactory.Transferred(interaction, context.TransferringAgentId, context.TransferringUserId, InteractionTransferType.Blind, InteractionTransferTargetType.External, context.TargetId, InteractionTransferHistory.SentToExternalNumber, now),
            cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        // The call has left the contact center, so it settles as transferred through provider truth like every other
        // ending: the agent's wrap-up, the talk time and the outcome are decided in the one place that decides them.
        // A settled call is what makes the agent leg's own hangup, which follows, a teardown rather than an ending.
        await _providerVoiceEventService.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = interaction.ProviderName,
            ProviderCallId = providerCallId,
            State = VoiceCallState.Transferred,
            OccurredUtc = now,
            IdempotencyKey = $"transfer-external:{interaction.ItemId}:{now.Ticks}",
        }, cancellationToken);

        await _agentRelease.HangUpAsync(interaction.ProviderName, agentLegs, cancellationToken);

        return TransferResult.Success("The call was transferred to the external number.");
    }
}
