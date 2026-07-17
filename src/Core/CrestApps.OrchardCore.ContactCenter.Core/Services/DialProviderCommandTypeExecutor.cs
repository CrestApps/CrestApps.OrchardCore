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
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Handles provider command execution for <see cref="ProviderCommandType.Dial"/> commands.
/// Deserializes the dial request, stamps idempotency metadata, routes the outbound call, and projects
/// outcomes onto the linked interaction and CRM activity.
/// </summary>
public sealed class DialProviderCommandTypeExecutor : IProviderCommandTypeExecutor
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IEnumerable<IProviderCommandDispatchValidator> _dispatchValidators;
    private readonly IVoiceContactCenterCallRouter _voiceCallRouter;
    private readonly IInteractionManager _interactionManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialProviderCommandTypeExecutor"/> class.
    /// </summary>
    /// <param name="dispatchValidators">The policy validators applied before recovering a pending dispatch.</param>
    /// <param name="voiceCallRouter">The router used to execute outbound voice commands.</param>
    /// <param name="interactionManager">The manager used to project interaction outcomes.</param>
    /// <param name="activityManager">The manager used to project CRM activity outcomes.</param>
    /// <param name="clock">The clock used to stamp UTC timestamps on projections.</param>
    /// <param name="callSessionManager">The call session manager used to persist first-command ownership.</param>
    /// <param name="agentManager">The agent profile manager used to resolve the dialing user.</param>
    public DialProviderCommandTypeExecutor(
        IEnumerable<IProviderCommandDispatchValidator> dispatchValidators,
        IVoiceContactCenterCallRouter voiceCallRouter,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        IClock clock,
        ICallSessionManager callSessionManager = null,
        IAgentProfileManager agentManager = null)
    {
        _dispatchValidators = dispatchValidators;
        _voiceCallRouter = voiceCallRouter;
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _agentManager = agentManager;
        _activityManager = activityManager;
        _clock = clock;
    }

    /// <inheritdoc/>
    public ProviderCommandType CommandType => ProviderCommandType.Dial;

    /// <inheritdoc/>
    public async Task<bool> CanDispatchAsync(ProviderCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RequestPayload))
        {
            return false;
        }

        var validated = false;

        foreach (var validator in _dispatchValidators)
        {
            validated = true;

            if (!await validator.CanDispatchAsync(command, cancellationToken))
            {
                return false;
            }
        }

        if (!validated)
        {
            return false;
        }

        var request = DeserializeDialRequest(command);

        if (_callSessionManager is not null &&
            !await IsAuthorizedFirstDialAsync(request, cancellationToken))
        {
            return false;
        }

        if (_callSessionManager is not null)
        {
            await EnsureOwnedDialSessionAsync(request, command, cancellationToken);
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

        var request = DeserializeDialRequest(command);

        StampRequest(request, command, claim);

        if (_callSessionManager is not null &&
            !await IsAuthorizedFirstDialAsync(request, cancellationToken))
        {
            return new ContactCenterVoiceProviderResult
            {
                Succeeded = false,
                OutcomeUnknown = true,
                ProviderName = command.ProviderName,
                ErrorCode = "dial_denied",
                ErrorMessage = "The requested dial is not available.",
            };
        }

        if (_callSessionManager is not null)
        {
            await EnsureOwnedDialSessionAsync(request, command, cancellationToken);
        }

        return await _voiceCallRouter.RouteOutboundAsync(request, command.ProviderName, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ProjectSuccessAsync(
        ProviderCommand command,
        ContactCenterVoiceProviderResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(result);

        // Resolve the owned session first so the interaction projection never diverges
        // from the session binding. A conflict (session already bound to a DIFFERENT call)
        // means the new dial outcome must be treated as unknown: do not repoint the
        // interaction's ProviderInteractionId at the new call id.
        var sessionConflict = false;
        CallSession ownedSession = null;

        if (_callSessionManager is not null &&
            !string.IsNullOrWhiteSpace(command.InteractionId) &&
            !string.IsNullOrWhiteSpace(result.ProviderCallId))
        {
            ownedSession = await _callSessionManager.FindByInteractionIdAsync(command.InteractionId, cancellationToken);

            if (ownedSession is not null &&
                !string.IsNullOrWhiteSpace(ownedSession.ProviderCallId) &&
                !string.Equals(ownedSession.ProviderCallId, result.ProviderCallId, StringComparison.Ordinal))
            {
                sessionConflict = true;
            }
        }

        if (!sessionConflict && !string.IsNullOrWhiteSpace(command.InteractionId))
        {
            var interaction = await _interactionManager.FindByIdAsync(command.InteractionId, cancellationToken);

            if (interaction is not null)
            {
                interaction.Status = InteractionStatus.Ringing;
                interaction.ProviderName = string.IsNullOrWhiteSpace(result.ProviderName)
                    ? command.ProviderName
                    : result.ProviderName;
                interaction.ProviderInteractionId = result.ProviderCallId;
                interaction.StartedUtc = _clock.UtcNow;
                await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
            }
        }

        if (!string.IsNullOrWhiteSpace(command.ActivityItemId))
        {
            var activity = await _activityManager.FindByIdAsync(command.ActivityItemId, cancellationToken);

            if (activity is not null)
            {
                activity.Status = ActivityStatus.Dialing;
                await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);
            }
        }

        if (!sessionConflict && ownedSession is not null && string.IsNullOrWhiteSpace(ownedSession.ProviderCallId))
        {
            ownedSession.ProviderCallId = result.ProviderCallId;
            ownedSession.ProviderName = string.IsNullOrWhiteSpace(result.ProviderName)
                ? command.ProviderName
                : result.ProviderName;
            ownedSession.State = ContactCenterCallState.Ringing;

            await _callSessionManager.UpdateAsync(ownedSession, cancellationToken: cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task ProjectFailureAsync(ProviderCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!string.IsNullOrWhiteSpace(command.InteractionId))
        {
            var interaction = await _interactionManager.FindByIdAsync(command.InteractionId, cancellationToken);

            if (interaction is not null)
            {
                interaction.Status = InteractionStatus.Failed;
                interaction.EndedUtc = _clock.UtcNow;
                await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
            }
        }

        if (!string.IsNullOrWhiteSpace(command.ActivityItemId))
        {
            var activity = await _activityManager.FindByIdAsync(command.ActivityItemId, cancellationToken);

            if (activity is not null)
            {
                activity.Status = ActivityStatus.Failed;
                await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);
            }
        }
    }

    /// <inheritdoc/>
    public async Task ProjectOutcomeUnknownAsync(
        ProviderCommand command,
        string errorCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!string.IsNullOrWhiteSpace(command.InteractionId))
        {
            var interaction = await _interactionManager.FindByIdAsync(command.InteractionId, cancellationToken);

            if (interaction is not null)
            {
                interaction.TechnicalMetadata["providerErrorCode"] = errorCode;
                await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
            }
        }

        if (!string.IsNullOrWhiteSpace(command.ActivityItemId))
        {
            var activity = await _activityManager.FindByIdAsync(command.ActivityItemId, cancellationToken);

            if (activity is not null)
            {
                activity.Status = ActivityStatus.Dialing;
                await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);
            }
        }
    }

    private static ContactCenterDialRequest DeserializeDialRequest(ProviderCommand command)
    {
        var request = JsonSerializer.Deserialize<ContactCenterDialRequest>(
            command.RequestPayload,
            _serializerOptions);

        return request ?? throw new JsonException("The provider command request payload deserialized to null.");
    }

    private static void StampRequest(
        ContactCenterDialRequest request,
        ProviderCommand command,
        ProviderCommandClaim claim)
    {
        request.CommandId = command.CommandId;
        request.Metadata ??= new Dictionary<string, string>();
        request.Metadata[ContactCenterConstants.CommandMetadata.CommandId] = command.CommandId;
        request.Metadata[TelephonyConstants.RequestMetadata.IdempotencyKey] = command.CommandId;
        request.Metadata[ContactCenterConstants.CommandMetadata.FenceToken] = claim.FenceToken.ToString(CultureInfo.InvariantCulture);
        request.Metadata[TelephonyConstants.RequestMetadata.FenceToken] = claim.FenceToken.ToString(CultureInfo.InvariantCulture);
    }

    private async Task<bool> IsAuthorizedFirstDialAsync(
        ContactCenterDialRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InteractionId) ||
            string.IsNullOrWhiteSpace(request.AgentId) ||
            string.IsNullOrWhiteSpace(request.AgentUserId) ||
            _agentManager is null ||
            !IsAllowedAddress(request.Destination) ||
            (!string.IsNullOrWhiteSpace(request.CallerId) && !IsAllowedAddress(request.CallerId)))
        {
            return false;
        }

        var agent = await _agentManager.FindByUserIdAsync(request.AgentUserId, cancellationToken);

        return agent is not null &&
            string.Equals(agent.ItemId, request.AgentId, StringComparison.Ordinal);
    }

    private async Task EnsureOwnedDialSessionAsync(
        ContactCenterDialRequest request,
        ProviderCommand command,
        CancellationToken cancellationToken)
    {
        var session = await _callSessionManager.FindByInteractionIdAsync(request.InteractionId, cancellationToken);

        if (session is not null)
        {
            return;
        }

        var now = _clock.UtcNow;
        session = await _callSessionManager.NewAsync(cancellationToken: cancellationToken);
        session.InteractionId = request.InteractionId;
        session.ActivityItemId = request.ActivityId;
        session.ProviderName = command.ProviderName;
        session.Direction = InteractionDirection.Outbound;
        session.DeliveryModel = VoiceProviderDeliveryModel.ServerSideAcd;
        session.AgentId = request.AgentId;
        session.QueueId = request.QueueId;
        session.FromAddress = request.CallerId;
        session.ToAddress = request.Destination;
        session.State = ContactCenterCallState.Planned;
        session.DurableCommandId = command.CommandId;
        session.CreatedUtc = now;

        await _callSessionManager.CreateAsync(session, cancellationToken: cancellationToken);
    }

    private static bool IsAllowedAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address) ||
            !address.StartsWith('+') ||
            address.Length < 8 ||
            address.Length > 16)
        {
            return false;
        }

        var digits = address.Substring(1);

        if (!digits.All(char.IsDigit))
        {
            return false;
        }

        return !digits.EndsWith("911", StringComparison.Ordinal) &&
            !digits.EndsWith("112", StringComparison.Ordinal) &&
            !digits.EndsWith("999", StringComparison.Ordinal) &&
            !digits.StartsWith("1900", StringComparison.Ordinal) &&
            !digits.StartsWith("1976", StringComparison.Ordinal) &&
            !digits.StartsWith("4470", StringComparison.Ordinal);
    }
}
