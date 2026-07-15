using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IProviderCommandStateService"/>. Each transition loads
/// the current command, validates it against the legal transition graph and any required fence and owner
/// tokens, then persists the change through the provider command manager and commits it in the tenant
/// session so the durable state and the fence advance atomically.
/// </summary>
public sealed class ProviderCommandStateService : IProviderCommandStateService
{
    private static readonly TimeSpan _registrationLockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _registrationLockExpiration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _initialRecoveryDelay = TimeSpan.FromMinutes(5);
    private static readonly Dictionary<ProviderCommandStatus, ProviderCommandStatus[]> _allowedTransitions =
        new Dictionary<ProviderCommandStatus, ProviderCommandStatus[]>
        {
            [ProviderCommandStatus.Pending] = [ProviderCommandStatus.Claimed, ProviderCommandStatus.Compensating, ProviderCommandStatus.Failed],
            [ProviderCommandStatus.Claimed] = [ProviderCommandStatus.Sent, ProviderCommandStatus.Pending, ProviderCommandStatus.Failed],
            [ProviderCommandStatus.Sent] = [ProviderCommandStatus.Confirmed, ProviderCommandStatus.OutcomeUnknown, ProviderCommandStatus.Compensating, ProviderCommandStatus.Failed],
            [ProviderCommandStatus.OutcomeUnknown] = [ProviderCommandStatus.Confirmed, ProviderCommandStatus.Compensating, ProviderCommandStatus.Paused, ProviderCommandStatus.Failed],
            [ProviderCommandStatus.Paused] = [ProviderCommandStatus.Confirmed, ProviderCommandStatus.Compensating, ProviderCommandStatus.OutcomeUnknown, ProviderCommandStatus.Failed],
            [ProviderCommandStatus.Compensating] = [ProviderCommandStatus.Compensated, ProviderCommandStatus.Failed],
            [ProviderCommandStatus.Confirmed] = [],
            [ProviderCommandStatus.Compensated] = [],
            [ProviderCommandStatus.Failed] = [],
        };

    private readonly IProviderCommandManager _manager;
    private readonly ISession _session;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderCommandStateService"/> class.
    /// </summary>
    /// <param name="manager">The provider command manager used to read and persist commands.</param>
    /// <param name="session">The tenant YesSql session used to commit each transition.</param>
    /// <param name="distributedLock">The distributed lock used to serialize command registration by idempotency key.</param>
    /// <param name="clock">The clock used to stamp transition times.</param>
    public ProviderCommandStateService(
        IProviderCommandManager manager,
        ISession session,
        IDistributedLock distributedLock,
        IClock clock)
    {
        _manager = manager;
        _session = session;
        _distributedLock = distributedLock;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<ProviderCommand> RegisterAsync(ProviderCommandRegistration registration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentException.ThrowIfNullOrEmpty(registration.CommandId);
        ArgumentException.ThrowIfNullOrEmpty(registration.ProviderName);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            $"ContactCenterProviderCommand:Register:{registration.CommandId}",
            _registrationLockTimeout,
            _registrationLockExpiration);

        if (!locked)
        {
            throw new InvalidOperationException($"The provider command '{registration.CommandId}' is currently being registered.");
        }

        await using var acquiredLock = locker;

        var existing = await _manager.FindByCommandIdAsync(registration.CommandId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var now = _clock.UtcNow;
        var command = await _manager.NewAsync(cancellationToken: cancellationToken);
        command.CommandId = registration.CommandId;
        command.ProviderName = registration.ProviderName;
        command.CommandType = registration.CommandType;
        command.ActivityItemId = registration.ActivityItemId;
        command.InteractionId = registration.InteractionId;
        command.ReservationId = registration.ReservationId;
        command.DialerProfileId = registration.DialerProfileId;
        command.RequestPayload = registration.RequestPayload;
        command.Status = ProviderCommandStatus.Pending;
        command.FenceToken = 0;
        command.OwnerToken = null;
        command.LeaseExpiresUtc = now;
        command.NextAttemptUtc = now.Add(_initialRecoveryDelay);
        command.CreatedUtc = now;
        command.ModifiedUtc = now;

        await _manager.CreateAsync(command, cancellationToken: cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return command;
    }

    /// <inheritdoc/>
    public async Task<ProviderCommandClaim> TryClaimAsync(string commandId, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(commandId);

        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "The lease duration must be positive.");
        }

        var command = await LoadRequiredAsync(commandId, cancellationToken);
        var now = _clock.UtcNow;
        var claimable = command.Status == ProviderCommandStatus.Pending ||
            (command.Status == ProviderCommandStatus.Claimed && command.LeaseExpiresUtc <= now);

        if (!claimable)
        {
            return null;
        }

        command.FenceToken += 1;
        command.OwnerToken = IdGenerator.GenerateId();
        command.LeaseExpiresUtc = now.Add(leaseDuration);
        command.Status = ProviderCommandStatus.Claimed;
        command.AttemptCount += 1;
        command.ModifiedUtc = now;

        try
        {
            await PersistAsync(command, cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return null;
        }

        return CreateClaim(command);
    }

    /// <inheritdoc/>
    public async Task<ProviderCommand> MarkSentAsync(string commandId, ProviderCommandClaim claim, string providerReference = null, CancellationToken cancellationToken = default)
    {
        var command = await LoadRequiredAsync(commandId, cancellationToken);
        var now = _clock.UtcNow;

        EnsureTransitionAllowed(command, ProviderCommandStatus.Sent);
        EnsureClaim(command, claim, now);

        command.Status = ProviderCommandStatus.Sent;
        command.SentUtc = now;
        command.ProviderReference = providerReference ?? command.ProviderReference;
        command.ModifiedUtc = now;

        await PersistAsync(command, cancellationToken);

        return command;
    }

    /// <inheritdoc/>
    public async Task<ProviderCommand> ConfirmSentAsync(string commandId, ProviderCommandClaim claim, string providerReference = null, CancellationToken cancellationToken = default)
    {
        var command = await StageConfirmSentAsync(commandId, claim, providerReference, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return command;
    }

    /// <inheritdoc/>
    public async Task<ProviderCommand> StageConfirmSentAsync(string commandId, ProviderCommandClaim claim, string providerReference = null, CancellationToken cancellationToken = default)
    {
        var command = await LoadRequiredAsync(commandId, cancellationToken);
        var now = _clock.UtcNow;

        EnsureTransitionAllowed(command, ProviderCommandStatus.Confirmed);
        EnsureClaim(command, claim, now);

        ApplyConfirmed(command, providerReference, now);

        await _manager.UpdateAsync(command, cancellationToken: cancellationToken);

        return command;
    }

    /// <inheritdoc/>
    public async Task<ProviderCommand> MarkOutcomeUnknownAsync(string commandId, ProviderCommandClaim claim, string reason = null, CancellationToken cancellationToken = default)
    {
        var command = await StageOutcomeUnknownAsync(commandId, claim, reason, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return command;
    }

    /// <inheritdoc/>
    public async Task<ProviderCommand> StageOutcomeUnknownAsync(string commandId, ProviderCommandClaim claim, string reason = null, CancellationToken cancellationToken = default)
    {
        var command = await LoadRequiredAsync(commandId, cancellationToken);
        var now = _clock.UtcNow;

        EnsureTransitionAllowed(command, ProviderCommandStatus.OutcomeUnknown);
        EnsureClaim(command, claim, now);

        command.Status = ProviderCommandStatus.OutcomeUnknown;
        command.LastError = reason;
        command.NextAttemptUtc = now;
        command.ModifiedUtc = now;

        await _manager.UpdateAsync(command, cancellationToken: cancellationToken);

        return command;
    }

    /// <inheritdoc/>
    public async Task<ProviderCommand> EscalateExpiredLeaseAsync(string commandId, CancellationToken cancellationToken = default)
    {
        var command = await LoadRequiredAsync(commandId, cancellationToken);
        var now = _clock.UtcNow;

        if (command.LeaseExpiresUtc > now)
        {
            return command;
        }

        var target = command.Status switch
        {
            ProviderCommandStatus.Sent => ProviderCommandStatus.OutcomeUnknown,
            ProviderCommandStatus.Claimed => ProviderCommandStatus.Pending,
            _ => (ProviderCommandStatus?)null,
        };

        if (target is null)
        {
            return command;
        }

        EnsureTransitionAllowed(command, target.Value);

        command.Status = target.Value;
        command.OwnerToken = null;
        command.NextAttemptUtc = now;
        command.ModifiedUtc = now;

        await PersistAsync(command, cancellationToken);

        return command;
    }

    /// <inheritdoc/>
    public async Task<ProviderCommandClaim> TryClaimReconciliationAsync(
        string commandId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(commandId);

        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "The lease duration must be positive.");
        }

        var command = await LoadRequiredAsync(commandId, cancellationToken);
        var now = _clock.UtcNow;

        if (command.Status != ProviderCommandStatus.OutcomeUnknown || command.LeaseExpiresUtc > now)
        {
            return null;
        }

        command.FenceToken += 1;
        command.OwnerToken = IdGenerator.GenerateId();
        command.LeaseExpiresUtc = now.Add(leaseDuration);
        command.ReconcileCount += 1;
        command.ModifiedUtc = now;

        try
        {
            await PersistAsync(command, cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return null;
        }

        return CreateClaim(command);
    }

    /// <inheritdoc/>
    public async Task<ProviderCommand> ConfirmFromReconciliationAsync(
        string commandId,
        ProviderCommandClaim claim,
        string providerReference = null,
        CancellationToken cancellationToken = default)
    {
        var command = await StageConfirmFromReconciliationAsync(commandId, claim, providerReference, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return command;
    }

    /// <inheritdoc/>
    public async Task<ProviderCommand> StageConfirmFromReconciliationAsync(
        string commandId,
        ProviderCommandClaim claim,
        string providerReference = null,
        CancellationToken cancellationToken = default)
    {
        var command = await LoadRequiredAsync(commandId, cancellationToken);
        var now = _clock.UtcNow;

        EnsureTransitionAllowed(command, ProviderCommandStatus.Confirmed);
        EnsureClaim(command, claim, now);

        ApplyConfirmed(command, providerReference, now);

        await _manager.UpdateAsync(command, cancellationToken: cancellationToken);

        return command;
    }

    /// <inheritdoc/>
    public async Task<ProviderCommand> BeginPendingCompensationAsync(string commandId, string reason = null, CancellationToken cancellationToken = default)
    {
        var command = await LoadRequiredAsync(commandId, cancellationToken);

        if (command.Status != ProviderCommandStatus.Pending)
        {
            throw new ProviderCommandTransitionException(
                command.CommandId,
                command.Status,
                ProviderCommandStatus.Compensating);
        }

        EnsureTransitionAllowed(command, ProviderCommandStatus.Compensating);

        var now = _clock.UtcNow;
        ApplyCompensating(command, reason, now);

        await PersistAsync(command, cancellationToken);

        return command;
    }

    /// <inheritdoc/>
    public async Task<ProviderCommand> BeginCompensationAsync(
        string commandId,
        ProviderCommandClaim claim,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        var command = await LoadRequiredAsync(commandId, cancellationToken);
        var now = _clock.UtcNow;

        EnsureTransitionAllowed(command, ProviderCommandStatus.Compensating);
        EnsureClaim(command, claim, now);

        ApplyCompensating(command, reason, now);
        await PersistAsync(command, cancellationToken);

        return command;
    }

    /// <inheritdoc/>
    public async Task<ProviderCommandClaim> TryClaimCompensationAsync(
        string commandId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(commandId);

        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "The lease duration must be positive.");
        }

        var command = await LoadRequiredAsync(commandId, cancellationToken);
        var now = _clock.UtcNow;

        if (command.Status != ProviderCommandStatus.Compensating || command.LeaseExpiresUtc > now)
        {
            return null;
        }

        command.FenceToken += 1;
        command.OwnerToken = IdGenerator.GenerateId();
        command.LeaseExpiresUtc = now.Add(leaseDuration);
        command.AttemptCount += 1;
        command.ModifiedUtc = now;

        try
        {
            await PersistAsync(command, cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return null;
        }

        return CreateClaim(command);
    }

    /// <inheritdoc/>
    public async Task<ProviderCommand> CompleteCompensationAsync(
        string commandId,
        ProviderCommandClaim claim,
        CancellationToken cancellationToken = default)
    {
        var command = await LoadRequiredAsync(commandId, cancellationToken);
        var now = _clock.UtcNow;

        EnsureTransitionAllowed(command, ProviderCommandStatus.Compensated);
        EnsureClaim(command, claim, now);

        command.Status = ProviderCommandStatus.Compensated;
        command.OwnerToken = null;
        command.LeaseExpiresUtc = now;
        command.CompletedUtc = now;
        command.ModifiedUtc = now;

        await PersistAsync(command, cancellationToken);

        return command;
    }

    /// <inheritdoc/>
    public async Task<ProviderCommand> PauseAsync(
        string commandId,
        ProviderCommandClaim claim,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        var command = await LoadRequiredAsync(commandId, cancellationToken);
        var now = _clock.UtcNow;

        EnsureTransitionAllowed(command, ProviderCommandStatus.Paused);
        EnsureClaim(command, claim, now);

        ApplyPaused(command, reason, now);
        await PersistAsync(command, cancellationToken);

        return command;
    }

    /// <inheritdoc/>
    public async Task<ProviderCommand> FailAsync(string commandId, string reason = null, CancellationToken cancellationToken = default)
    {
        var command = await LoadRequiredAsync(commandId, cancellationToken);

        EnsureTransitionAllowed(command, ProviderCommandStatus.Failed);

        var now = _clock.UtcNow;
        command.Status = ProviderCommandStatus.Failed;
        command.LastError = reason;
        command.CompletedUtc = now;
        command.ModifiedUtc = now;

        await PersistAsync(command, cancellationToken);

        return command;
    }

    private static void ApplyConfirmed(ProviderCommand command, string providerReference, DateTime now)
    {
        command.Status = ProviderCommandStatus.Confirmed;
        command.ProviderReference = providerReference ?? command.ProviderReference;
        command.CompletedUtc = now;
        command.ModifiedUtc = now;
    }

    private static void ApplyCompensating(ProviderCommand command, string reason, DateTime now)
    {
        command.Status = ProviderCommandStatus.Compensating;
        command.LastError = reason;
        command.OwnerToken = null;
        command.LeaseExpiresUtc = now;
        command.NextAttemptUtc = now;
        command.ModifiedUtc = now;
    }

    private static void ApplyPaused(ProviderCommand command, string reason, DateTime now)
    {
        command.Status = ProviderCommandStatus.Paused;
        command.LastError = reason;
        command.OwnerToken = null;
        command.LeaseExpiresUtc = now;
        command.ModifiedUtc = now;
    }

    private static ProviderCommandClaim CreateClaim(ProviderCommand command)
    {
        return new ProviderCommandClaim
        {
            CommandId = command.CommandId,
            FenceToken = command.FenceToken,
            OwnerToken = command.OwnerToken,
            LeaseExpiresUtc = command.LeaseExpiresUtc,
        };
    }

    private static void EnsureTransitionAllowed(ProviderCommand command, ProviderCommandStatus target)
    {
        if (!_allowedTransitions.TryGetValue(command.Status, out var allowed) || !allowed.Contains(target))
        {
            throw new ProviderCommandTransitionException(command.CommandId, command.Status, target);
        }
    }

    private static void EnsureClaim(ProviderCommand command, ProviderCommandClaim claim, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(claim);

        if (command.FenceToken != claim.FenceToken ||
            !string.Equals(command.OwnerToken, claim.OwnerToken, StringComparison.Ordinal) ||
            command.LeaseExpiresUtc <= now)
        {
            throw new ProviderCommandFenceException(command.CommandId, command.FenceToken, claim.FenceToken);
        }
    }

    private async Task<ProviderCommand> LoadRequiredAsync(string commandId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(commandId);

        var command = await _manager.FindByCommandIdAsync(commandId, cancellationToken);

        if (command is null)
        {
            throw new InvalidOperationException($"The provider command '{commandId}' does not exist.");
        }

        return command;
    }

    private async Task PersistAsync(ProviderCommand command, CancellationToken cancellationToken)
    {
        await _manager.UpdateAsync(command, cancellationToken: cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
    }
}
