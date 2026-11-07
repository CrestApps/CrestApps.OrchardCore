using CrestApps.OrchardCore.Retention;
using Microsoft.Extensions.Logging;
using YesSql;

namespace CrestApps.OrchardCore.YesSql.Core.Retention;

/// <summary>
/// Runs retention policies: drains each policy a batch at a time, commits every batch before reading the next, and
/// reports honestly when the budget or a failure stopped a policy short instead of truncating silently.
/// </summary>
public static class RetentionRunner
{
    /// <summary>
    /// Runs one retention cycle over the policies.
    /// </summary>
    /// <param name="policies">The policies, run in order.</param>
    /// <param name="session">The tenant session, committed after every batch.</param>
    /// <param name="nowUtc">The current UTC time the cutoffs are computed from.</param>
    /// <param name="batchSize">The most records one batch removes.</param>
    /// <param name="batchBudget">The most batches one policy may run in the cycle.</param>
    /// <param name="logger">The logger failures are written to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>What the cycle did.</returns>
    public static async Task<RetentionReport> RunAsync(
        IEnumerable<IRetentionPolicy> policies,
        ISession session,
        DateTime nowUtc,
        int batchSize,
        int batchBudget,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(logger);

        var all = policies as IReadOnlyList<IRetentionPolicy> ?? policies.ToList();
        var report = new RetentionReport();

        foreach (var policy in all)
        {
            // The budget is per policy. Sharing one budget across every policy would let whichever policy is
            // registered first consume all of it, and the records behind it would never be removed at all.
            var remainingBatches = batchBudget;
            var result = new RetentionPolicyResult
            {
                Name = policy.Name,
            };

            report.Policies.Add(result);

            if (!policy.TryGetCutoff(nowUtc, out var cutoffUtc))
            {
                continue;
            }

            result.IsEnabled = true;
            result.CutoffUtc = cutoffUtc;

            while (true)
            {
                if (remainingBatches <= 0 || cancellationToken.IsCancellationRequested)
                {
                    result.WorkRemains = true;

                    break;
                }

                int purged;

                try
                {
                    purged = await policy.PurgeBatchAsync(cutoffUtc, batchSize, cancellationToken);
                }
                catch (RetentionBatchException ex)
                {
                    // The failed batch staged a mix of completed records and the failing record's partial side effects
                    // into the shared session, and the session cannot commit only the completed records. Discard the
                    // whole batch so nothing partial is ever committed; every record it touched is retried cleanly on
                    // the next cycle, and one unhealthy policy does not stop the others.
                    logger.LogError(ex, "Retention failed while purging {PolicyName}; the batch was discarded and will be retried on the next cycle.", policy.Name);
                    result.WorkRemains = true;

                    try
                    {
                        await session.ResetAsync();
                    }
                    catch (Exception resetException)
                    {
                        // A session that cannot even be reset is unusable, so stop the cycle rather than let the next
                        // policy run against a poisoned session and appear to purge while its deletes are discarded.
                        logger.LogError(resetException, "Retention could not reset the session after a failed batch for {PolicyName}. The cycle was stopped because the session cannot be reused.", policy.Name);
                        MarkUnvisitedPoliciesAsUnfinished(all, report, nowUtc);

                        return report;
                    }

                    break;
                }
                catch (Exception ex)
                {
                    // The batch failed before it staged any work (reading the expired records), so the session is
                    // still usable and the remaining policies can drain normally.
                    logger.LogError(ex, "Retention failed while reading the expired records of {PolicyName}.", policy.Name);
                    result.WorkRemains = true;

                    break;
                }

                remainingBatches--;

                if (purged == 0)
                {
                    break;
                }

                try
                {
                    await session.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    // A failed flush cancels the session permanently, so every later policy would appear to purge while
                    // its deletes were silently discarded. Stop the cycle instead.
                    logger.LogError(ex, "Retention failed while committing the purged records of {PolicyName}. The cycle was stopped because the session cannot be reused.", policy.Name);
                    result.WorkRemains = true;
                    MarkUnvisitedPoliciesAsUnfinished(all, report, nowUtc);

                    return report;
                }

                // Counted only once the deletes have committed, so a failed flush cannot over-report.
                result.PurgedCount += purged;

                if (purged < batchSize)
                {
                    break;
                }
            }
        }

        return report;
    }

    private static void MarkUnvisitedPoliciesAsUnfinished(IReadOnlyList<IRetentionPolicy> policies, RetentionReport report, DateTime nowUtc)
    {
        var visited = report.Policies.Select(policy => policy.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var policy in policies)
        {
            if (visited.Contains(policy.Name))
            {
                continue;
            }

            // A policy whose window is disabled removes nothing, so it is never reported as unfinished.
            var isEnabled = policy.TryGetCutoff(nowUtc, out var cutoffUtc);

            report.Policies.Add(new RetentionPolicyResult
            {
                Name = policy.Name,
                IsEnabled = isEnabled,
                CutoffUtc = isEnabled ? cutoffUtc : null,
                WorkRemains = isEnabled,
            });
        }
    }
}
