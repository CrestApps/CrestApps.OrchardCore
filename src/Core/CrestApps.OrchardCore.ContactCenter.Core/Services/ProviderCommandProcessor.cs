using System.Globalization;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IProviderCommandProcessor"/>. It durably records a
/// command as sent before provider execution and reconciles uncertain outcomes instead of reissuing them.
/// </summary>
public sealed class ProviderCommandProcessor : IProviderCommandProcessor
{
    private const int MaxRecoveryBatchSize = 25;
    private static readonly TimeSpan _leaseDuration = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IProviderCommandManager _commandManager;
    private readonly IProviderCommandStateService _stateService;
    private readonly IActivityReservationService _reservationService;
    private readonly IInteractionManager _interactionManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IVoiceContactCenterCallRouter _voiceCallRouter;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly IEnumerable<IProviderCommandDispatchValidator> _dispatchValidators;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderCommandProcessor"/> class.
    /// </summary>
    /// <param name="commandManager">The manager used to load due commands.</param>
    /// <param name="stateService">The durable command state machine.</param>
    /// <param name="reservationService">The reservation service used for definitive-failure compensation.</param>
    /// <param name="interactionManager">The manager used to project interaction outcomes.</param>
    /// <param name="activityManager">The manager used to project CRM activity outcomes.</param>
    /// <param name="voiceCallRouter">The router used to execute outbound voice commands.</param>
    /// <param name="voiceProviderResolver">The resolver used to find optional provider reconciliation support.</param>
    /// <param name="dispatchValidators">The policy validators applied before recovering pending dispatch.</param>
    /// <param name="scopeExecutor">The executor used to isolate each recovery transition in a fresh shell scope.</param>
    /// <param name="session">The tenant YesSql session used to commit outcome projections.</param>
    /// <param name="clock">The clock used to determine recovery windows.</param>
    /// <param name="logger">The logger instance.</param>
    public ProviderCommandProcessor(
        IProviderCommandManager commandManager,
        IProviderCommandStateService stateService,
        IActivityReservationService reservationService,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        IVoiceContactCenterCallRouter voiceCallRouter,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IEnumerable<IProviderCommandDispatchValidator> dispatchValidators,
        IContactCenterScopeExecutor scopeExecutor,
        ISession session,
        IClock clock,
        ILogger<ProviderCommandProcessor> logger)
    {
        _commandManager = commandManager;
        _stateService = stateService;
        _reservationService = reservationService;
        _interactionManager = interactionManager;
        _activityManager = activityManager;
        _voiceCallRouter = voiceCallRouter;
        _voiceProviderResolver = voiceProviderResolver;
        _dispatchValidators = dispatchValidators;
        _scopeExecutor = scopeExecutor;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ProviderCommand> DispatchAsync(string commandId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(commandId);

        var command = await _commandManager.FindByCommandIdAsync(commandId, cancellationToken);

        if (command is null)
        {
            throw new InvalidOperationException($"The provider command '{commandId}' does not exist.");
        }

        return command.Status switch
        {
            ProviderCommandStatus.Pending => await DispatchPendingAsync(command, cancellationToken),
            ProviderCommandStatus.OutcomeUnknown => await ReconcileAsync(command, cancellationToken),
            ProviderCommandStatus.Compensating => await CompensateAsync(command, cancellationToken),
            _ => command,
        };
    }

    /// <inheritdoc/>
    public async Task<int> RecoverDueAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var candidateIds = new HashSet<string>(StringComparer.Ordinal);
        var reclaimable = await _commandManager.ListReclaimableAsync(
            now,
            MaxRecoveryBatchSize,
            cancellationToken);

        foreach (var command in reclaimable)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var commandId = command.CommandId;

            await _scopeExecutor.ExecuteAsync<IProviderCommandStateService>(async stateService =>
            {
                try
                {
                    var escalated = await stateService.EscalateExpiredLeaseAsync(commandId, cancellationToken);

                    if (IsRecoverable(escalated))
                    {
                        candidateIds.Add(commandId);
                    }
                }
                catch (ConcurrencyException)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            "Skipped expired provider command '{ProviderCommandId}' because another worker won recovery.",
                            commandId);
                    }
                }
                catch (ProviderCommandTransitionException)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            "Skipped expired provider command '{ProviderCommandId}' because its state changed during recovery.",
                            commandId);
                    }
                }
            });
        }

        var dueCommands = await _commandManager.ListDueAsync(
            now,
            MaxRecoveryBatchSize,
            cancellationToken);

        foreach (var command in dueCommands)
        {
            candidateIds.Add(command.CommandId);
        }

        var attempted = 0;

        foreach (var commandId in candidateIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await _scopeExecutor.ExecuteAsync<IProviderCommandProcessor>(processor =>
                    processor.DispatchAsync(commandId, cancellationToken));
                attempted++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (ConcurrencyException)
            {
                continue;
            }
            catch (ProviderCommandTransitionException)
            {
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Unable to recover provider command '{ProviderCommandId}' because processing failed with {ExceptionType}.",
                    commandId,
                    ex.GetType().Name);
            }
        }

        return attempted;
    }

    private async Task<ProviderCommand> DispatchPendingAsync(
        ProviderCommand command,
        CancellationToken cancellationToken)
    {
        if (!await CanDispatchAsync(command, cancellationToken))
        {
            var ineligibleCompensation = await _stateService.BeginPendingCompensationAsync(
                command.CommandId,
                "The command is no longer eligible for outbound dispatch.",
                cancellationToken);

            return await CompensateAsync(ineligibleCompensation, cancellationToken);
        }

        ContactCenterDialRequest request;

        try
        {
            request = DeserializeDialRequest(command);
        }
        catch (JsonException)
        {
            var invalidPayloadCompensation = await _stateService.BeginPendingCompensationAsync(
                command.CommandId,
                "The provider command request payload is invalid.",
                cancellationToken);

            return await CompensateAsync(invalidPayloadCompensation, cancellationToken);
        }

        var claim = await _stateService.TryClaimAsync(command.CommandId, _leaseDuration, cancellationToken);

        if (claim is null)
        {
            return command;
        }

        StampRequest(request, command, claim);
        try
        {
            await _stateService.MarkSentAsync(command.CommandId, claim, cancellationToken: cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return command;
        }
        catch (ProviderCommandTransitionException)
        {
            return await _commandManager.FindByCommandIdAsync(command.CommandId, cancellationToken) ?? command;
        }

        ContactCenterVoiceProviderResult result;

        try
        {
            result = await _voiceCallRouter.RouteOutboundAsync(request, command.ProviderName, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await MarkOutcomeUnknownAfterSendAsync(
                command,
                claim,
                "Provider dispatch was cancelled after the command was sent.",
                "provider_dispatch_cancelled",
                CancellationToken.None);

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Provider command '{ProviderCommandId}' returned no reliable result because dispatch failed with {ExceptionType}.",
                command.CommandId,
                ex.GetType().Name);

            return await MarkOutcomeUnknownAfterSendAsync(
                command,
                claim,
                "Provider dispatch did not return a reliable result.",
                "provider_dispatch_failed",
                cancellationToken);
        }

        if (result?.Succeeded == true && !string.IsNullOrWhiteSpace(result.ProviderCallId))
        {
            ProviderCommand confirmed;

            try
            {
                confirmed = await _stateService.StageConfirmSentAsync(
                    command.CommandId,
                    claim,
                    result.ProviderCallId,
                    cancellationToken);
            }
            catch (ProviderCommandFenceException ex)
            {
                _logger.LogWarning(
                    "Ignored a stale provider success for command '{ProviderCommandId}' with fence {FenceToken}; a newer owner controls settlement.",
                    command.CommandId,
                    ex.ProvidedFenceToken);

                return await _commandManager.FindByCommandIdAsync(command.CommandId, cancellationToken) ?? command;
            }
            catch (ConcurrencyException)
            {
                return command;
            }
            catch (ProviderCommandTransitionException)
            {
                return await _commandManager.FindByCommandIdAsync(command.CommandId, CancellationToken.None) ?? command;
            }

            await UpdateSuccessProjectionAsync(
                confirmed,
                string.IsNullOrWhiteSpace(result.ProviderName) ? command.ProviderName : result.ProviderName,
                result.ProviderCallId,
                cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            return confirmed;
        }

        if (result is null || result.OutcomeUnknown || result.Succeeded)
        {
            return await MarkOutcomeUnknownAfterSendAsync(
                command,
                claim,
                result?.Succeeded == true
                    ? "The provider did not return a call identifier."
                    : "The provider could not prove the command outcome.",
                result?.ErrorCode ?? "provider_outcome_unknown",
                cancellationToken);
        }

        var compensating = await _stateService.BeginCompensationAsync(
            command.CommandId,
            claim,
            result.ErrorCode ?? "The provider rejected the command.",
            cancellationToken);

        return await CompensateAsync(compensating, cancellationToken);
    }

    private async Task<ProviderCommand> ReconcileAsync(ProviderCommand command, CancellationToken cancellationToken)
    {
        var claim = await _stateService.TryClaimReconciliationAsync(
            command.CommandId,
            _leaseDuration,
            cancellationToken);

        if (claim is null)
        {
            return command;
        }

        var provider = _voiceProviderResolver.Get(command.ProviderName);

        if (provider is not IContactCenterVoiceCommandReconciler reconciler)
        {
            return await _stateService.PauseAsync(
                command.CommandId,
                claim,
                "The provider does not support command reconciliation.",
                cancellationToken);
        }

        ContactCenterVoiceCommandReconciliationResult result;

        try
        {
            result = await reconciler.ReconcileCommandAsync(command.CommandId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Provider command '{ProviderCommandId}' reconciliation failed with {ExceptionType}.",
                command.CommandId,
                ex.GetType().Name);

            return await _stateService.PauseAsync(
                command.CommandId,
                claim,
                "The provider could not reconcile the command outcome.",
                cancellationToken);
        }

        if (result?.Outcome == ContactCenterVoiceCommandReconciliationOutcome.Confirmed)
        {
            var confirmed = await _stateService.StageConfirmFromReconciliationAsync(
                command.CommandId,
                claim,
                result.ProviderCallId,
                cancellationToken);

            await UpdateSuccessProjectionAsync(
                confirmed,
                command.ProviderName,
                result.ProviderCallId ?? confirmed.ProviderReference,
                cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            return confirmed;
        }

        if (result?.Outcome == ContactCenterVoiceCommandReconciliationOutcome.NotExecuted)
        {
            var compensating = await _stateService.BeginCompensationAsync(
                command.CommandId,
                claim,
                result.Message ?? "The provider confirmed that the command did not execute.",
                cancellationToken);

            return await CompensateAsync(compensating, cancellationToken);
        }

        return await _stateService.PauseAsync(
            command.CommandId,
            claim,
            result?.Message ?? "The provider could not prove the command outcome.",
            cancellationToken);
    }

    private async Task<ProviderCommand> CompensateAsync(ProviderCommand command, CancellationToken cancellationToken)
    {
        var claim = await _stateService.TryClaimCompensationAsync(
            command.CommandId,
            _leaseDuration,
            cancellationToken);

        if (claim is null)
        {
            return command;
        }

        await CompensateReservationAsync(command, cancellationToken);
        await UpdateFailureProjectionAsync(command, cancellationToken);

        return await _stateService.CompleteCompensationAsync(command.CommandId, claim, cancellationToken);
    }

    private async Task CompensateReservationAsync(ProviderCommand command, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.ReservationId))
        {
            await _reservationService.CompensateAsync(command.ReservationId, true, cancellationToken);
        }
    }

    private async Task UpdateSuccessProjectionAsync(
        ProviderCommand command,
        string providerName,
        string providerCallId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.InteractionId))
        {
            var interaction = await _interactionManager.FindByIdAsync(command.InteractionId, cancellationToken);

            if (interaction is not null)
            {
                interaction.Status = InteractionStatus.Ringing;
                interaction.ProviderName = providerName;
                interaction.ProviderInteractionId = providerCallId;
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

    private async Task UpdateFailureProjectionAsync(ProviderCommand command, CancellationToken cancellationToken)
    {
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

    private async Task<ProviderCommand> MarkOutcomeUnknownAfterSendAsync(
        ProviderCommand command,
        ProviderCommandClaim claim,
        string reason,
        string errorCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var outcomeUnknown = await _stateService.StageOutcomeUnknownAsync(
                command.CommandId,
                claim,
                reason,
                cancellationToken);

            await UpdateUnknownProjectionAsync(outcomeUnknown, errorCode, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            return outcomeUnknown;
        }
        catch (ProviderCommandFenceException ex)
        {
            _logger.LogWarning(
                "Ignored a stale unknown outcome for command '{ProviderCommandId}' with fence {FenceToken}; a newer owner controls reconciliation.",
                command.CommandId,
                ex.ProvidedFenceToken);

            return await _commandManager.FindByCommandIdAsync(command.CommandId, CancellationToken.None) ?? command;
        }
        catch (ConcurrencyException)
        {
            return command;
        }
    }

    private async Task UpdateUnknownProjectionAsync(
        ProviderCommand command,
        string errorCode,
        CancellationToken cancellationToken)
    {
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

    private async Task<bool> CanDispatchAsync(ProviderCommand command, CancellationToken cancellationToken)
    {
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

    private static bool IsRecoverable(ProviderCommand command)
    {
        return command?.Status is ProviderCommandStatus.Pending
            or ProviderCommandStatus.OutcomeUnknown
            or ProviderCommandStatus.Compensating;
    }

    private static ContactCenterDialRequest DeserializeDialRequest(ProviderCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.RequestPayload))
        {
            throw new JsonException("The provider command request payload is empty.");
        }

        var request = JsonSerializer.Deserialize<ContactCenterDialRequest>(
            command.RequestPayload,
            _serializerOptions);

        if (request is null)
        {
            throw new JsonException("The provider command request payload is empty.");
        }

        return request;
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
