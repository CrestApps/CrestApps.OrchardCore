using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;

/// <summary>
/// Evaluates the health of a durable message queue (outbox or provider ingress) from its dead-letter and overdue
/// backlog counts. The decision is a pure function of the counts and configured thresholds so it can be unit
/// tested without a database, and so every queue-backed health check shares one consistent contract.
/// </summary>
public static class BacklogHealthEvaluator
{
    /// <summary>
    /// Evaluates a queue's health from its current backlog signals.
    /// </summary>
    /// <param name="subsystem">The human-readable subsystem name used in the health result description.</param>
    /// <param name="deadLetterCount">The number of dead-lettered messages requiring operator intervention.</param>
    /// <param name="overdueCount">The number of pending or claimed messages already past their due time.</param>
    /// <param name="options">The configured thresholds.</param>
    /// <returns>The resulting <see cref="HealthCheckResult"/>.</returns>
    public static HealthCheckResult Evaluate(
        string subsystem,
        int deadLetterCount,
        int overdueCount,
        ContactCenterHealthCheckOptions options)
    {
        ArgumentException.ThrowIfNullOrEmpty(subsystem);
        ArgumentNullException.ThrowIfNull(options);

        options.Normalize();

        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["deadLettered"] = deadLetterCount,
            ["overdue"] = overdueCount,
        };

        if (deadLetterCount >= options.DeadLetterUnhealthyThreshold ||
            overdueCount >= options.OverdueBacklogUnhealthyThreshold)
        {
            return new HealthCheckResult(
                HealthStatus.Unhealthy,
                $"{subsystem} is unhealthy: {deadLetterCount} dead-lettered, {overdueCount} overdue.",
                data: data);
        }

        if (deadLetterCount >= options.DeadLetterDegradedThreshold ||
            overdueCount >= options.OverdueBacklogDegradedThreshold)
        {
            return new HealthCheckResult(
                HealthStatus.Degraded,
                $"{subsystem} is degraded: {deadLetterCount} dead-lettered, {overdueCount} overdue.",
                data: data);
        }

        return new HealthCheckResult(
            HealthStatus.Healthy,
            $"{subsystem} is healthy.",
            data: data);
    }
}
