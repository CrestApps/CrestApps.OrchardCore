using System.Globalization;
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
    public DialProviderCommandTypeExecutor(
        IEnumerable<IProviderCommandDispatchValidator> dispatchValidators,
        IVoiceContactCenterCallRouter voiceCallRouter,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        IClock clock)
    {
        _dispatchValidators = dispatchValidators;
        _voiceCallRouter = voiceCallRouter;
        _interactionManager = interactionManager;
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

        return validated;
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

        if (!string.IsNullOrWhiteSpace(command.InteractionId))
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
}
