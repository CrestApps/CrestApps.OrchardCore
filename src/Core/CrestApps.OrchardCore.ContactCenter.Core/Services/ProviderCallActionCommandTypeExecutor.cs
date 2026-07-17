using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the shared implementation for durable provider commands that act on an existing call.
/// </summary>
public abstract class ProviderCallActionCommandTypeExecutor : IProviderCommandTypeExecutor
{
    private const string OwnerMetadataKey = "providerCommandOwner";

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ITelephonyService _telephonyService;
    private readonly IInteractionManager _interactionManager;
    private readonly ICallControlAuthorizationService _callControlAuthorizationService;
    private readonly IActivityQueueService _queueService;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderCallActionCommandTypeExecutor"/> class.
    /// </summary>
    /// <param name="telephonyServices">The optional telephony services used to execute the provider action.</param>
    /// <param name="interactionManager">The interaction manager used to validate and project linked interactions.</param>
    /// <param name="queueService">The queue service used to restore live work after a definitive action failure.</param>
    /// <param name="activityManager">The CRM activity manager used to restore live work after a definitive action failure.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="clock">The clock used to stamp projections.</param>
    /// <param name="callControlAuthorizationService">The shared call-control authorization boundary.</param>
    protected ProviderCallActionCommandTypeExecutor(
        IEnumerable<ITelephonyService> telephonyServices,
        IInteractionManager interactionManager,
        IActivityQueueService queueService,
        IOmnichannelActivityManager activityManager,
        IContactCenterEventPublisher publisher,
        IClock clock,
        ICallControlAuthorizationService callControlAuthorizationService = null)
    {
        _telephonyService = telephonyServices.FirstOrDefault();
        _interactionManager = interactionManager;
        _callControlAuthorizationService = callControlAuthorizationService;
        _queueService = queueService;
        _activityManager = activityManager;
        _publisher = publisher;
        _clock = clock;
    }

    /// <inheritdoc/>
    public abstract ProviderCommandType CommandType { get; }

    /// <inheritdoc/>
    public async Task<bool> CanDispatchAsync(ProviderCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.CommandType != CommandType ||
            _telephonyService is null)
        {
            return false;
        }

        var request = DeserializeRequest(command.RequestPayload);

        if (request is null ||
            string.IsNullOrWhiteSpace(request.ProviderCallId) ||
            string.IsNullOrWhiteSpace(command.InteractionId))
        {
            return false;
        }

        var interaction = await _interactionManager.FindByIdAsync(command.InteractionId, cancellationToken);

        if (interaction is null || IsTerminal(interaction.Status))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(interaction.ProviderInteractionId) &&
            !string.Equals(interaction.ProviderInteractionId, request.ProviderCallId, StringComparison.Ordinal))
        {
            return false;
        }

        if (_callControlAuthorizationService is null)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(request.AgentUserId))
        {
            return false;
        }

        var authorization = await _callControlAuthorizationService.AuthorizeAsync(new CallControlAuthorizationContext
        {
            UserId = request.AgentUserId,
            Verb = CommandType == ProviderCommandType.SendToVoicemail
                ? CallControlVerb.Voicemail
                : CallControlVerb.Decline,
            InteractionId = command.InteractionId,
            ProviderName = command.ProviderName,
            ProviderCallId = request.ProviderCallId,
        }, cancellationToken);

        return authorization.Succeeded;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> ExecuteAsync(
        ProviderCommand command,
        ProviderCommandClaim claim,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(claim);

        var request = DeserializeRequest(command.RequestPayload);

        if (request is null ||
            string.IsNullOrWhiteSpace(request.ProviderCallId))
        {
            return CreateUnknownResult(command, request?.ProviderCallId, "invalid_request", "The provider call action request could not be deserialized.");
        }

        if (_telephonyService is null)
        {
            return CreateUnknownResult(command, request.ProviderCallId, GetErrorCodePrefix("unavailable"), "No telephony service is registered.");
        }

        var resolvedProviderCallId = request.ProviderCallId;

        if (_callControlAuthorizationService is not null)
        {
            if (string.IsNullOrWhiteSpace(request.AgentUserId))
            {
                return CreateUnknownResult(command, request.ProviderCallId, GetErrorCodePrefix("denied"), "The requested call is not available.");
            }

            var authorization = await _callControlAuthorizationService.AuthorizeAsync(new CallControlAuthorizationContext
            {
                UserId = request.AgentUserId,
                Verb = CommandType == ProviderCommandType.SendToVoicemail
                    ? CallControlVerb.Voicemail
                    : CallControlVerb.Decline,
                InteractionId = command.InteractionId,
                ProviderName = command.ProviderName,
                ProviderCallId = request.ProviderCallId,
            }, cancellationToken);

            if (!authorization.Succeeded)
            {
                return CreateUnknownResult(command, request.ProviderCallId, GetErrorCodePrefix("denied"), authorization.FailureReason);
            }

            resolvedProviderCallId = authorization.ProviderCallId;
        }

        var call = new CallReference
        {
            CallId = resolvedProviderCallId,
            Metadata = NormalizeMetadata(request.Metadata),
        };

        call.Metadata[ContactCenterConstants.CommandMetadata.CommandId] = claim.CommandId;
        call.Metadata[ContactCenterConstants.CommandMetadata.FenceToken] = claim.FenceToken.ToString(CultureInfo.InvariantCulture);
        call.Metadata[OwnerMetadataKey] = claim.OwnerToken;
        var result = await ExecuteTelephonyAsync(_telephonyService, call, cancellationToken);

        return ToVoiceProviderResult(command, request, result);
    }

    /// <inheritdoc/>
    public async Task ProjectSuccessAsync(
        ProviderCommand command,
        ContactCenterVoiceProviderResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(result);

        var request = DeserializeRequest(command.RequestPayload);

        if (request is null)
        {
            return;
        }

        var interaction = await GetInteractionAsync(command.InteractionId, cancellationToken);

        if (interaction is null)
        {
            return;
        }

        var now = _clock.UtcNow;
        interaction.Status = InteractionStatus.Ended;
        interaction.EndedUtc ??= now;
        ApplyProjectionMetadata(interaction, command, request, "Succeeded", null, null, now);
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        await _publisher.PublishAsync(CreateCallEndedEvent(command, interaction, request, now), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ProjectFailureAsync(ProviderCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = DeserializeRequest(command.RequestPayload);
        var interaction = await GetInteractionAsync(command.InteractionId, cancellationToken);

        if (request?.ReofferOnFailure == true &&
            interaction is not null &&
            !IsTerminal(interaction.Status) &&
            !string.IsNullOrWhiteSpace(request.ActivityItemId) &&
            !string.IsNullOrWhiteSpace(request.QueueId))
        {
            await _queueService.EnqueueAsync(request.ActivityItemId, request.QueueId, null, cancellationToken);
            var activity = await _activityManager.FindByIdAsync(request.ActivityItemId, cancellationToken);

            if (activity is not null)
            {
                activity.AssignmentStatus = ActivityAssignmentStatus.Available;
                activity.Status = ActivityStatus.AwaitingAgentResponse;
                activity.CompletedUtc = null;
                await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);
            }

            interaction.Status = InteractionStatus.Created;
            interaction.EndedUtc = null;
            ApplyProjectionMetadata(
                interaction,
                command,
                request,
                "FailedRequeued",
                GetErrorCodePrefix("failed"),
                "The provider action failed and the live call was returned to routing.",
                _clock.UtcNow);
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
            await _publisher.PublishAsync(CreateOfferRequeuedEvent(command, request), cancellationToken);

            return;
        }

        await ProjectDiagnosticAsync(
            command,
            "Failed",
            GetErrorCodePrefix("failed"),
            "The provider action failed.",
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ProjectOutcomeUnknownAsync(
        ProviderCommand command,
        string errorCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await ProjectDiagnosticAsync(
            command,
            "Unknown",
            errorCode,
            "The provider could not prove the provider action outcome.",
            cancellationToken);
    }

    /// <summary>
    /// Executes the provider-specific telephony action.
    /// </summary>
    /// <param name="telephonyService">The telephony service used to execute the action.</param>
    /// <param name="call">The call reference and metadata for the action.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The telephony result.</returns>
    protected abstract Task<TelephonyResult> ExecuteTelephonyAsync(
        ITelephonyService telephonyService,
        CallReference call,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the display name used in diagnostic metadata.
    /// </summary>
    protected abstract string ActionName { get; }

    /// <summary>
    /// Gets the prefix used when constructing provider-facing error codes.
    /// </summary>
    protected abstract string ErrorCodePrefix { get; }

    private async Task ProjectDiagnosticAsync(
        ProviderCommand command,
        string outcome,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var request = DeserializeRequest(command.RequestPayload);

        if (request is null)
        {
            return;
        }

        var interaction = await GetInteractionAsync(command.InteractionId, cancellationToken);

        if (interaction is null)
        {
            return;
        }

        ApplyProjectionMetadata(interaction, command, request, outcome, errorCode, errorMessage, _clock.UtcNow);
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
    }

    private static ProviderCallActionCommandRequest DeserializeRequest(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ProviderCallActionCommandRequest>(payload, _serializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static bool IsTerminal(InteractionStatus status)
    {
        return status is InteractionStatus.Ended or InteractionStatus.Failed;
    }

    private static ContactCenterVoiceProviderResult CreateUnknownResult(
        ProviderCommand command,
        string providerCallId,
        string errorCode,
        string errorMessage)
    {
        return new ContactCenterVoiceProviderResult
        {
            Succeeded = false,
            OutcomeUnknown = true,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ProviderCallId = providerCallId ?? string.Empty,
            ProviderName = command.ProviderName ?? string.Empty,
        };
    }

    private ContactCenterVoiceProviderResult ToVoiceProviderResult(
        ProviderCommand command,
        ProviderCallActionCommandRequest request,
        TelephonyResult result)
    {
        if (result is null)
        {
            return CreateUnknownResult(
                command,
                request.ProviderCallId,
                GetErrorCodePrefix("outcome_unknown"),
                "The provider did not return a result.");
        }

        return new ContactCenterVoiceProviderResult
        {
            Succeeded = result.Succeeded,
            OutcomeUnknown = result.OutcomeUnknown,
            ErrorCode = result.OutcomeUnknown
                ? GetErrorCodePrefix("outcome_unknown")
                : result.Succeeded
                    ? null
                    : GetErrorCodePrefix("failed"),
            ErrorMessage = result.Error,
            ProviderCallId = request.ProviderCallId,
            ProviderName = command.ProviderName ?? string.Empty,
        };
    }

    private async Task<Interaction> GetInteractionAsync(string interactionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(interactionId))
        {
            return null;
        }

        return await _interactionManager.FindByIdAsync(interactionId, cancellationToken);
    }

    private static Dictionary<string, object> NormalizeMetadata(IDictionary<string, object> metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return [];
        }

        var normalized = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in metadata)
        {
            normalized[entry.Key] = NormalizeValue(entry.Value);
        }

        return normalized;
    }

    private static object NormalizeValue(object value)
    {
        if (value is not JsonElement jsonElement)
        {
            return value;
        }

        return NormalizeJsonElement(jsonElement);
    }

    private static object NormalizeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => element.EnumerateArray().Select(NormalizeJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                property => property.Name,
                property => NormalizeJsonElement(property.Value),
                StringComparer.OrdinalIgnoreCase),
            _ => element.ToString(),
        };
    }

    private InteractionEvent CreateCallEndedEvent(
        ProviderCommand command,
        Interaction interaction,
        ProviderCallActionCommandRequest request,
        DateTime occurredUtc)
    {
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.CallEnded,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(Interaction),
            AggregateId = interaction.ItemId,
            CorrelationId = interaction.CorrelationId,
            CausationId = command.CommandId,
            ActorId = string.IsNullOrWhiteSpace(command.ProviderName)
                ? ContactCenterConstants.SystemActor
                : command.ProviderName,
            SourceComponent = ContactCenterConstants.Components.CallSessions,
            OccurredUtc = occurredUtc,
            IdempotencyKey = command.CommandId,
        };

        interactionEvent.SetData(new Dictionary<string, object>
        {
            ["action"] = ActionName,
            ["providerCallId"] = request.ProviderCallId ?? string.Empty,
            ["providerName"] = command.ProviderName ?? string.Empty,
            ["metadata"] = NormalizeMetadata(request.Metadata),
        });

        return interactionEvent;
    }

    private static InteractionEvent CreateOfferRequeuedEvent(
        ProviderCommand command,
        ProviderCallActionCommandRequest request)
    {
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.OfferRequeued,
            InteractionId = command.InteractionId,
            AggregateType = nameof(ActivityReservation),
            AggregateId = command.ActivityItemId,
            ActorId = ContactCenterConstants.SystemActor,
            SourceComponent = ContactCenterConstants.Components.Voice,
            IdempotencyKey = ContactCenterClaimKeys.BuildProviderDomainEventIdempotencyKey(
                command.CommandId,
                ContactCenterConstants.Events.OfferRequeued),
        };

        interactionEvent.SetData(new OfferDeclinedEventData
        {
            QueueId = request.QueueId,
        });

        return interactionEvent;
    }

    private static void ApplyProjectionMetadata(
        Interaction interaction,
        ProviderCommand command,
        ProviderCallActionCommandRequest request,
        string outcome,
        string errorCode,
        string errorMessage,
        DateTime projectedUtc)
    {
        interaction.TechnicalMetadata["providerCallActionCommandId"] = command.CommandId;
        interaction.TechnicalMetadata["providerCallActionType"] = command.CommandType.ToString();
        interaction.TechnicalMetadata["providerCallActionOutcome"] = outcome;
        interaction.TechnicalMetadata["providerCallActionProviderCallId"] = request.ProviderCallId ?? string.Empty;
        interaction.TechnicalMetadata["providerCallActionProviderName"] = command.ProviderName ?? string.Empty;
        interaction.TechnicalMetadata["providerCallActionProjectedUtc"] = projectedUtc;
        interaction.TechnicalMetadata["providerCallActionMetadata"] = NormalizeMetadata(request.Metadata);

        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            interaction.TechnicalMetadata["providerCallActionErrorCode"] = errorCode;
        }

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            interaction.TechnicalMetadata["providerCallActionErrorMessage"] = errorMessage;
        }
    }

    private string GetErrorCodePrefix(string suffix)
    {
        return $"{ErrorCodePrefix}_{suffix}";
    }
}
