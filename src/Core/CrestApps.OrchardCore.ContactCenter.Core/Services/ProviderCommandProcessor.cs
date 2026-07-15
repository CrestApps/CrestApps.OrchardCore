using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IProviderCommandProcessor"/>. It durably records a
/// command as sent before provider execution and reconciles uncertain outcomes instead of reissuing them.
/// Each <see cref="ProviderCommandType"/> is handled by a uniquely registered
/// <see cref="IProviderCommandTypeExecutor"/>; a missing or duplicate registration causes safe compensation
/// without any provider contact.
/// </summary>
public sealed class ProviderCommandProcessor : IProviderCommandProcessor
{
    private const int MaxRecoveryBatchSize = 25;
    private static readonly TimeSpan _leaseDuration = TimeSpan.FromMinutes(5);

    private readonly IProviderCommandManager _commandManager;
    private readonly IProviderCommandStateService _stateService;
    private readonly IActivityReservationService _reservationService;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly IEnumerable<IProviderCommandTypeExecutor> _executors;
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
    /// <param name="voiceProviderResolver">The resolver used to find optional provider reconciliation support.</param>
    /// <param name="executors">The typed executors that handle per-command-type provider dispatch and projections.</param>
    /// <param name="scopeExecutor">The executor used to isolate each recovery transition in a fresh shell scope.</param>
    /// <param name="session">The tenant YesSql session used to commit outcome projections.</param>
    /// <param name="clock">The clock used to determine recovery windows.</param>
    /// <param name="logger">The logger instance.</param>
    public ProviderCommandProcessor(
        IProviderCommandManager commandManager,
        IProviderCommandStateService stateService,
        IActivityReservationService reservationService,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IEnumerable<IProviderCommandTypeExecutor> executors,
        IContactCenterScopeExecutor scopeExecutor,
        ISession session,
        IClock clock,
        ILogger<ProviderCommandProcessor> logger)
    {
        _commandManager = commandManager;
        _stateService = stateService;
        _reservationService = reservationService;
        _voiceProviderResolver = voiceProviderResolver;
        _executors = executors;
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

    /// <inheritdoc/>
    public async Task<ProviderCommand> SettleDispatchAsync(
        string commandId,
        ProviderCommandClaim claim,
        ContactCenterVoiceProviderResult result,
        string outcomeUnknownReason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(commandId);
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentException.ThrowIfNullOrEmpty(outcomeUnknownReason);

        var command = await _commandManager.FindByCommandIdAsync(commandId, cancellationToken);

        if (command is null)
        {
            throw new InvalidOperationException($"The provider command '{commandId}' does not exist.");
        }

        if (result?.Succeeded == true && !string.IsNullOrWhiteSpace(result.ProviderCallId))
        {
            var confirmed = await _stateService.StageConfirmSentAsync(
                commandId,
                claim,
                result.ProviderCallId,
                cancellationToken);
            var executor = ResolveExecutor(command);

            if (executor is not null)
            {
                await executor.ProjectSuccessAsync(confirmed, result, cancellationToken);
            }

            await _session.SaveChangesAsync(cancellationToken);

            return confirmed;
        }

        if (result is null || result.OutcomeUnknown || result.Succeeded)
        {
            var outcomeUnknown = await _stateService.StageOutcomeUnknownAsync(
                commandId,
                claim,
                outcomeUnknownReason,
                cancellationToken);
            var executor = ResolveExecutor(command);

            if (executor is not null)
            {
                await executor.ProjectOutcomeUnknownAsync(
                    outcomeUnknown,
                    result?.ErrorCode ?? "provider_outcome_unknown",
                    cancellationToken);
            }

            await _session.SaveChangesAsync(cancellationToken);

            return outcomeUnknown;
        }

        var compensating = await _stateService.BeginCompensationAsync(
            commandId,
            claim,
            result.ErrorCode ?? "The provider rejected the command.",
            cancellationToken);

        return await CompensateAsync(compensating, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ProviderCommand> SettleReconciliationAsync(
        string commandId,
        ProviderCommandClaim claim,
        ContactCenterVoiceCommandReconciliationResult result,
        string inconclusiveReason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(commandId);
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentException.ThrowIfNullOrEmpty(inconclusiveReason);

        var command = await _commandManager.FindByCommandIdAsync(commandId, cancellationToken);

        if (command is null)
        {
            throw new InvalidOperationException($"The provider command '{commandId}' does not exist.");
        }

        if (result?.Outcome == ContactCenterVoiceCommandReconciliationOutcome.Confirmed)
        {
            var confirmed = await _stateService.StageConfirmFromReconciliationAsync(
                commandId,
                claim,
                result.ProviderCallId,
                cancellationToken);
            var executor = ResolveExecutor(command);

            if (executor is not null)
            {
                var syntheticResult = new ContactCenterVoiceProviderResult
                {
                    Succeeded = true,
                    ProviderCallId = result.ProviderCallId ?? confirmed.ProviderReference,
                    ProviderName = command.ProviderName,
                };

                await executor.ProjectSuccessAsync(confirmed, syntheticResult, cancellationToken);
            }

            await _session.SaveChangesAsync(cancellationToken);

            return confirmed;
        }

        if (result?.Outcome == ContactCenterVoiceCommandReconciliationOutcome.NotExecuted)
        {
            var compensating = await _stateService.BeginCompensationAsync(
                commandId,
                claim,
                result.Message ?? "The provider confirmed that the command did not execute.",
                cancellationToken);

            return await CompensateAsync(compensating, cancellationToken);
        }

        return await _stateService.PauseAsync(
            commandId,
            claim,
            result?.Message ?? inconclusiveReason,
            cancellationToken);
    }

    private async Task<ProviderCommand> DispatchPendingAsync(
        ProviderCommand command,
        CancellationToken cancellationToken)
    {
        var executor = ResolveExecutor(command);

        if (executor is null)
        {
            _logger.LogError(
                "No unique executor is registered for command type '{CommandType}'. Command '{CommandId}' will be compensated without provider contact.",
                command.CommandType,
                command.CommandId);

            var unsupportedCompensation = await _stateService.BeginPendingCompensationAsync(
                command.CommandId,
                $"No executor is registered for command type '{command.CommandType}'.",
                cancellationToken);

            return await CompensateAsync(unsupportedCompensation, cancellationToken);
        }

        if (!await executor.CanDispatchAsync(command, cancellationToken))
        {
            var ineligibleCompensation = await _stateService.BeginPendingCompensationAsync(
                command.CommandId,
                "The command is no longer eligible for outbound dispatch.",
                cancellationToken);

            return await CompensateAsync(ineligibleCompensation, cancellationToken);
        }

        var claim = await _stateService.TryClaimAsync(command.CommandId, _leaseDuration, cancellationToken);

        if (claim is null)
        {
            return command;
        }

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
            result = await executor.ExecuteAsync(command, claim, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await SettleDispatchInFreshScopeAsync(
                command.CommandId,
                claim,
                new ContactCenterVoiceProviderResult
                {
                    OutcomeUnknown = true,
                    ErrorCode = "provider_dispatch_cancelled",
                },
                "Provider dispatch was cancelled after the command was sent.",
                CancellationToken.None);

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Provider command '{ProviderCommandId}' returned no reliable result because dispatch failed with {ExceptionType}.",
                command.CommandId,
                ex.GetType().Name);

            return await SettleDispatchInFreshScopeAsync(
                command.CommandId,
                claim,
                new ContactCenterVoiceProviderResult
                {
                    OutcomeUnknown = true,
                    ErrorCode = "provider_dispatch_failed",
                },
                "Provider dispatch did not return a reliable result.",
                cancellationToken);
        }

        return await SettleDispatchInFreshScopeAsync(
            command.CommandId,
            claim,
            result,
            result?.Succeeded == true
                ? "The provider did not return a call identifier."
                : "The provider could not prove the command outcome.",
            cancellationToken);
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
            return await SettleReconciliationInFreshScopeAsync(
                command.CommandId,
                claim,
                null,
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

            return await SettleReconciliationInFreshScopeAsync(
                command.CommandId,
                claim,
                null,
                "The provider could not reconcile the command outcome.",
                cancellationToken);
        }

        return await SettleReconciliationInFreshScopeAsync(
            command.CommandId,
            claim,
            result,
            "The provider could not prove the command outcome.",
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

        var executor = ResolveExecutor(command);

        if (executor is not null)
        {
            await executor.ProjectFailureAsync(command, cancellationToken);
        }

        return await _stateService.CompleteCompensationAsync(command.CommandId, claim, cancellationToken);
    }

    private async Task CompensateReservationAsync(ProviderCommand command, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.ReservationId))
        {
            await _reservationService.CompensateAsync(
                command.ReservationId,
                command.RemoveReservationFromQueueOnFailure,
                cancellationToken);
        }
    }

    private async Task<ProviderCommand> SettleDispatchInFreshScopeAsync(
        string commandId,
        ProviderCommandClaim claim,
        ContactCenterVoiceProviderResult result,
        string outcomeUnknownReason,
        CancellationToken cancellationToken)
    {
        ProviderCommand settled = null;

        try
        {
            await _scopeExecutor.ExecuteAsync<IProviderCommandProcessor>(async processor =>
            {
                settled = await processor.SettleDispatchAsync(
                    commandId,
                    claim,
                    result,
                    outcomeUnknownReason,
                    cancellationToken);
            });

            return settled;
        }
        catch (ProviderCommandFenceException ex)
        {
            _logger.LogWarning(
                "Ignored a stale provider response for command '{ProviderCommandId}' with fence {FenceToken}; a newer owner controls settlement.",
                commandId,
                ex.ProvidedFenceToken);

            return await _commandManager.FindByCommandIdAsync(commandId, CancellationToken.None);
        }
        catch (ConcurrencyException)
        {
            return await _commandManager.FindByCommandIdAsync(commandId, CancellationToken.None);
        }
        catch (ProviderCommandTransitionException)
        {
            return await _commandManager.FindByCommandIdAsync(commandId, CancellationToken.None);
        }
    }

    private async Task<ProviderCommand> SettleReconciliationInFreshScopeAsync(
        string commandId,
        ProviderCommandClaim claim,
        ContactCenterVoiceCommandReconciliationResult result,
        string inconclusiveReason,
        CancellationToken cancellationToken)
    {
        ProviderCommand settled = null;

        try
        {
            await _scopeExecutor.ExecuteAsync<IProviderCommandProcessor>(async processor =>
            {
                settled = await processor.SettleReconciliationAsync(
                    commandId,
                    claim,
                    result,
                    inconclusiveReason,
                    cancellationToken);
            });

            return settled;
        }
        catch (ProviderCommandFenceException ex)
        {
            _logger.LogWarning(
                "Ignored a stale provider reconciliation response for command '{ProviderCommandId}' with fence {FenceToken}; a newer owner controls settlement.",
                commandId,
                ex.ProvidedFenceToken);

            return await _commandManager.FindByCommandIdAsync(commandId, CancellationToken.None);
        }
        catch (ConcurrencyException)
        {
            return await _commandManager.FindByCommandIdAsync(commandId, CancellationToken.None);
        }
        catch (ProviderCommandTransitionException)
        {
            return await _commandManager.FindByCommandIdAsync(commandId, CancellationToken.None);
        }
    }

    private static bool IsRecoverable(ProviderCommand command)
    {
        return command?.Status is ProviderCommandStatus.Pending
            or ProviderCommandStatus.OutcomeUnknown
            or ProviderCommandStatus.Compensating;
    }

    private IProviderCommandTypeExecutor ResolveExecutor(ProviderCommand command)
    {
        IProviderCommandTypeExecutor found = null;
        var hasDuplicate = false;

        foreach (var executor in _executors)
        {
            if (executor.CommandType != command.CommandType)
            {
                continue;
            }

            if (found is not null)
            {
                hasDuplicate = true;
                break;
            }

            found = executor;
        }

        return hasDuplicate ? null : found;
    }
}
