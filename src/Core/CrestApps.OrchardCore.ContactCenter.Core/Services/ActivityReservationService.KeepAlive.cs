using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Keeping a pending offer with the agent who is still working on it.
/// </summary>
public sealed partial class ActivityReservationService
{
    /// <inheritdoc/>
    public async Task<DateTime?> ExtendPendingAsync(string activityItemId, string userId, int seconds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityItemId);
        ArgumentException.ThrowIfNullOrEmpty(userId);

        if (seconds <= 0)
        {
            return null;
        }

        var reservations = await _reservationManager.GetActiveByActivityAsync(activityItemId, cancellationToken);

        foreach (var candidate in reservations ?? [])
        {
            if (candidate.Status != ReservationStatus.Pending || string.IsNullOrEmpty(candidate.AgentId))
            {
                continue;
            }

            // The same lock the expiry takes, so an offer is either extended or expired, never both.
            (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
                GetReservationLockKey(candidate.ItemId),
                _coordinationOptions.ReservationLockTimeout,
                _coordinationOptions.ReservationLockExpiration);

            if (!locked)
            {
                continue;
            }

            await using var acquiredLock = locker;

            var reservation = await _reservationManager.FindByIdAsync(candidate.ItemId, cancellationToken);

            if (reservation is null || reservation.Status != ReservationStatus.Pending)
            {
                continue;
            }

            var agent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);

            if (agent is null || !string.Equals(agent.UserId, userId, StringComparison.Ordinal))
            {
                continue;
            }

            var expiresUtc = _clock.UtcNow.AddSeconds(seconds);

            if (reservation.ExpiresUtc >= expiresUtc)
            {
                return reservation.ExpiresUtc;
            }

            reservation.ExpiresUtc = expiresUtc;
            await _reservationManager.UpdateAsync(reservation, cancellationToken: cancellationToken);

            await _workStateService.MutateAsync(activityItemId, workState =>
            {
                if (string.Equals(workState.ReservationId, reservation.ItemId, StringComparison.Ordinal))
                {
                    workState.ReservationExpiresUtc = expiresUtc;
                }
            }, cancellationToken);

            await CommitTransitionAsync(activityItemId, reservation.AgentId, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Extended preview offer '{ReservationId}' of activity '{ActivityItemId}' for agent '{AgentId}' to {ExpiresUtc:O} while they work on it.",
                    reservation.ItemId.SanitizeLogValue(),
                    activityItemId.SanitizeLogValue(),
                    reservation.AgentId.SanitizeLogValue(),
                    expiresUtc);
            }

            return expiresUtc;
        }

        return null;
    }
}
