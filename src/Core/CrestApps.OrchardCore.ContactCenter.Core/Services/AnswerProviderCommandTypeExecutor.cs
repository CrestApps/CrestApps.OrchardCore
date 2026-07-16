using System.Globalization;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Handles provider-command execution for <see cref="ProviderCommandType.Answer"/> commands.
/// It connects the accepted inbound call through the live provider when available, falls back to the
/// default telephony facade when no voice provider is resolved, and projects the resulting call state
/// onto the interaction and call session models.
/// </summary>
public sealed class AnswerProviderCommandTypeExecutor : IProviderCommandTypeExecutor
{
    private const string OwnerMetadataKey = "providerCommandOwner";

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly ITelephonyService _telephonyService;
    private readonly IInteractionManager _interactionManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnswerProviderCommandTypeExecutor"/> class.
    /// </summary>
    /// <param name="voiceProviderResolver">The resolver used to locate the live voice provider.</param>
    /// <param name="telephonyService">The fallback telephony service used when no live voice provider exists.</param>
    /// <param name="interactionManager">The manager used to load and update the interaction projection.</param>
    /// <param name="callSessionManager">The manager used to load and update the call session projection.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="clock">The clock used to stamp UTC projections.</param>
    public AnswerProviderCommandTypeExecutor(
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        ITelephonyService telephonyService,
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        IContactCenterEventPublisher publisher,
        IClock clock)
    {
        _voiceProviderResolver = voiceProviderResolver;
        _telephonyService = telephonyService;
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _publisher = publisher;
        _clock = clock;
    }

    /// <inheritdoc/>
    public ProviderCommandType CommandType => ProviderCommandType.Answer;

    /// <inheritdoc/>
    public async Task<bool> CanDispatchAsync(ProviderCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.CommandType != ProviderCommandType.Answer ||
            string.IsNullOrWhiteSpace(command.ProviderName) ||
            string.IsNullOrWhiteSpace(command.RequestPayload))
        {
            return false;
        }

        var request = TryDeserializeRequest(command.RequestPayload);

        if (request is null ||
            string.IsNullOrWhiteSpace(request.ActivityId) ||
            string.IsNullOrWhiteSpace(request.InteractionId) ||
            string.IsNullOrWhiteSpace(request.ProviderCallId) ||
            string.IsNullOrWhiteSpace(request.AgentId) ||
            string.IsNullOrWhiteSpace(request.AgentUserId) ||
            string.IsNullOrWhiteSpace(request.QueueId))
        {
            return false;
        }

        var interaction = await _interactionManager.FindByIdAsync(request.InteractionId, cancellationToken);

        if (interaction is null || IsTerminal(interaction.Status) ||
            !string.Equals(interaction.ProviderInteractionId, request.ProviderCallId, StringComparison.Ordinal))
        {
            return false;
        }

        var session = await _callSessionManager.FindByInteractionIdAsync(request.InteractionId, cancellationToken);

        if (session is null || IsTerminal(session.State) ||
            !string.Equals(session.ProviderCallId, request.ProviderCallId, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> ExecuteAsync(
        ProviderCommand command,
        ProviderCommandClaim claim,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(claim);

        var request = DeserializeRequest(command);

        if (request is null)
        {
            throw new JsonException("The provider command request payload could not be deserialized.");
        }

        if (string.IsNullOrWhiteSpace(request.ProviderCallId))
        {
            throw new InvalidOperationException("The provider answer request is missing a provider call identifier.");
        }

        if (string.IsNullOrWhiteSpace(request.InteractionId) ||
            string.IsNullOrWhiteSpace(request.ActivityId) ||
            string.IsNullOrWhiteSpace(request.AgentId) ||
            string.IsNullOrWhiteSpace(request.AgentUserId) ||
            string.IsNullOrWhiteSpace(request.QueueId))
        {
            throw new InvalidOperationException("The provider answer request is incomplete.");
        }

        var provider = _voiceProviderResolver.Get(command.ProviderName);

        if (provider is IContactCenterVoiceCallControlProvider callControlProvider)
        {
            var providerRequest = CreateProviderConnectRequest(request, claim);
            var providerResult = await callControlProvider.ConnectToAgentAsync(providerRequest, cancellationToken);

            return NormalizeProviderResult(
                providerResult,
                ResolveProviderName(command.ProviderName, provider.TechnicalName),
                request.ProviderCallId);
        }

        if (_telephonyService is null)
        {
            throw new InvalidOperationException("No telephony service is available to answer the inbound call.");
        }

        var callReference = CreateCallReference(request, claim);
        var telephonyResult = await _telephonyService.AnswerAsync(callReference, cancellationToken);

        return ConvertTelephonyResult(telephonyResult, command.ProviderName, request.ProviderCallId);
    }

    /// <inheritdoc/>
    public async Task ProjectSuccessAsync(
        ProviderCommand command,
        ContactCenterVoiceProviderResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(result);

        var request = DeserializeRequest(command);

        if (request is null)
        {
            throw new JsonException("The provider command request payload could not be deserialized.");
        }

        var now = _clock.UtcNow;
        var providerName = ResolveProviderName(command.ProviderName, result.ProviderName);
        var providerCallId = ResolveProviderCallId(request.ProviderCallId, result.ProviderCallId);

        var interaction = await _interactionManager.FindByIdAsync(command.InteractionId, cancellationToken);

        if (interaction is not null)
        {
            interaction.AgentId = request.AgentId;
            interaction.QueueId = request.QueueId;
            interaction.ProviderName = providerName;
            interaction.ProviderInteractionId = providerCallId;
            interaction.Status = InteractionStatus.Connected;
            interaction.StartedUtc ??= now;
            interaction.AnsweredUtc = now;

            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        }

        var session = await _callSessionManager.FindByInteractionIdAsync(command.InteractionId, cancellationToken);

        if (session is not null)
        {
            session.AgentId = request.AgentId;
            session.QueueId = request.QueueId;
            session.ProviderName = providerName;
            session.ProviderCallId = providerCallId;
            session.State = ContactCenterCallState.Connected;
            session.StartedUtc ??= now;
            session.AnsweredUtc = now;

            await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
        }

        await PublishAsync(
            ContactCenterConstants.Events.CallConnected,
            nameof(Interaction),
            command.InteractionId,
            request.AgentId,
            command.CommandId,
            command.InteractionId,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ProjectFailureAsync(ProviderCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = DeserializeRequest(command);

        if (request is null)
        {
            throw new JsonException("The provider command request payload could not be deserialized.");
        }

        var now = _clock.UtcNow;
        var interaction = await _interactionManager.FindByIdAsync(command.InteractionId, cancellationToken);

        if (interaction is not null)
        {
            interaction.Status = request.ReofferOnFailure
                ? InteractionStatus.Ringing
                : InteractionStatus.Failed;
            interaction.EndedUtc = request.ReofferOnFailure ? null : now;

            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        }

        var session = await _callSessionManager.FindByInteractionIdAsync(command.InteractionId, cancellationToken);

        if (session is not null)
        {
            session.State = request.ReofferOnFailure
                ? ContactCenterCallState.Ringing
                : ContactCenterCallState.Ended;
            session.EndedUtc = request.ReofferOnFailure ? null : now;

            await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
        }

        if (request.ReofferOnFailure)
        {
            await PublishAsync(
                ContactCenterConstants.Events.OfferRequeued,
                nameof(ActivityReservation),
                string.IsNullOrWhiteSpace(command.ReservationId) ? command.ActivityItemId : command.ReservationId,
                request.AgentId,
                command.CommandId,
                command.InteractionId,
                cancellationToken,
                new OfferDeclinedEventData
                {
                    QueueId = request.QueueId,
                });

            return;
        }

        await PublishAsync(
            ContactCenterConstants.Events.CallEnded,
            nameof(Interaction),
            command.InteractionId,
            request.AgentId,
            command.CommandId,
            command.InteractionId,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ProjectOutcomeUnknownAsync(
        ProviderCommand command,
        string errorCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = DeserializeRequest(command);

        if (request is null)
        {
            throw new JsonException("The provider command request payload could not be deserialized.");
        }

        var providerErrorCode = string.IsNullOrWhiteSpace(errorCode)
            ? "provider_answer_unknown"
            : errorCode;

        var interaction = await _interactionManager.FindByIdAsync(command.InteractionId, cancellationToken);

        if (interaction is not null)
        {
            interaction.TechnicalMetadata["providerErrorCode"] = providerErrorCode;
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        }

        var session = await _callSessionManager.FindByInteractionIdAsync(command.InteractionId, cancellationToken);

        if (session is not null)
        {
            session.Metadata["providerErrorCode"] = providerErrorCode;
            await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
        }
    }

    private static ContactCenterConnectRequest CreateProviderConnectRequest(
        ProviderAnswerCommandRequest request,
        ProviderCommandClaim claim)
    {
        var connectRequest = new ContactCenterConnectRequest
        {
            ActivityId = request.ActivityId,
            InteractionId = request.InteractionId,
            ProviderCallId = request.ProviderCallId,
            AgentId = request.AgentId,
            AgentUserId = request.AgentUserId,
            QueueId = request.QueueId,
        };

        StampMetadata(connectRequest.Metadata, claim);

        return connectRequest;
    }

    private static CallReference CreateCallReference(
        ProviderAnswerCommandRequest request,
        ProviderCommandClaim claim)
    {
        var callReference = new CallReference
        {
            CallId = request.ProviderCallId,
            Metadata = new Dictionary<string, object>(),
        };

        StampMetadata(callReference.Metadata, claim);

        return callReference;
    }

    private static void StampMetadata(IDictionary<string, string> metadata, ProviderCommandClaim claim)
    {
        metadata[ContactCenterConstants.CommandMetadata.CommandId] = claim.CommandId;
        metadata[ContactCenterConstants.CommandMetadata.FenceToken] = claim.FenceToken.ToString(CultureInfo.InvariantCulture);
        metadata[OwnerMetadataKey] = claim.OwnerToken;
    }

    private static void StampMetadata(IDictionary<string, object> metadata, ProviderCommandClaim claim)
    {
        metadata[ContactCenterConstants.CommandMetadata.CommandId] = claim.CommandId;
        metadata[ContactCenterConstants.CommandMetadata.FenceToken] = claim.FenceToken.ToString(CultureInfo.InvariantCulture);
        metadata[OwnerMetadataKey] = claim.OwnerToken;
    }

    private static ContactCenterVoiceProviderResult ConvertTelephonyResult(
        TelephonyResult result,
        string providerName,
        string providerCallId)
    {
        if (result is null)
        {
            return new ContactCenterVoiceProviderResult
            {
                Succeeded = false,
                OutcomeUnknown = true,
                ProviderName = providerName,
                ProviderCallId = providerCallId,
                ErrorMessage = "The telephony service did not return a result.",
            };
        }

        return new ContactCenterVoiceProviderResult
        {
            Succeeded = result.Succeeded,
            OutcomeUnknown = result.OutcomeUnknown,
            ProviderName = providerName,
            ProviderCallId = result.Call?.CallId ?? providerCallId,
            ErrorMessage = result.Error,
        };
    }

    private static ContactCenterVoiceProviderResult NormalizeProviderResult(
        ContactCenterVoiceProviderResult result,
        string providerName,
        string providerCallId)
    {
        if (result is null)
        {
            return new ContactCenterVoiceProviderResult
            {
                Succeeded = false,
                OutcomeUnknown = true,
                ProviderName = providerName,
                ProviderCallId = providerCallId,
                ErrorMessage = "The provider did not return a result.",
            };
        }

        if (string.IsNullOrWhiteSpace(result.ProviderName))
        {
            result.ProviderName = providerName;
        }

        if (string.IsNullOrWhiteSpace(result.ProviderCallId))
        {
            result.ProviderCallId = providerCallId;
        }

        return result;
    }

    private static string ResolveProviderName(string commandProviderName, string resultProviderName)
    {
        return string.IsNullOrWhiteSpace(resultProviderName)
            ? commandProviderName
            : resultProviderName;
    }

    private static string ResolveProviderCallId(string requestProviderCallId, string resultProviderCallId)
    {
        return string.IsNullOrWhiteSpace(resultProviderCallId)
            ? requestProviderCallId
            : resultProviderCallId;
    }

    private static ProviderAnswerCommandRequest TryDeserializeRequest(string requestPayload)
    {
        if (string.IsNullOrWhiteSpace(requestPayload))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ProviderAnswerCommandRequest>(requestPayload, _serializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ProviderAnswerCommandRequest DeserializeRequest(ProviderCommand command)
    {
        return TryDeserializeRequest(command.RequestPayload);
    }

    private static bool IsTerminal(InteractionStatus status)
    {
        return status is InteractionStatus.Ended or InteractionStatus.Failed;
    }

    private static bool IsTerminal(ContactCenterCallState state)
    {
        return state is ContactCenterCallState.Ended or
            ContactCenterCallState.Failed or
            ContactCenterCallState.NoAnswer or
            ContactCenterCallState.Rejected or
            ContactCenterCallState.Canceled or
            ContactCenterCallState.Transferred;
    }

    private Task PublishAsync(
        string eventType,
        string aggregateType,
        string aggregateId,
        string actorId,
        string commandId,
        string interactionId,
        CancellationToken cancellationToken,
        object data = null)
    {
        var interactionEvent = new InteractionEvent
        {
            EventType = eventType,
            InteractionId = interactionId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            ActorId = actorId,
            SourceComponent = ContactCenterConstants.Components.Voice,
            IdempotencyKey = ContactCenterClaimKeys.BuildProviderDomainEventIdempotencyKey(commandId, eventType),
        };

        if (data is not null)
        {
            interactionEvent.SetData(data);
        }

        return _publisher.PublishAsync(interactionEvent, cancellationToken);
    }
}
