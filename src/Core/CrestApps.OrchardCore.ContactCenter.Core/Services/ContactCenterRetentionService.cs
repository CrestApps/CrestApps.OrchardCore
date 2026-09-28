using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterRetentionService"/>. It iterates the
/// registered retention policies rather than hard-coding a purge per table, drains each entity until it is
/// empty, and reports honestly when the cycle budget stopped it short instead of truncating silently.
/// </summary>
public sealed class ContactCenterRetentionService : IContactCenterRetentionService
{
    private readonly IEnumerable<IContactCenterRetentionPolicy> _policies;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ContactCenterRetentionOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterRetentionService"/> class.
    /// </summary>
    /// <param name="policies">The registered retention policies, one per high-volume table.</param>
    /// <param name="session">The tenant YesSql session, committed between batches to bound transaction size.</param>
    /// <param name="clock">The clock used to compute cutoffs.</param>
    /// <param name="options">The configured retention options.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterRetentionService(
        IEnumerable<IContactCenterRetentionPolicy> policies,
        ISession session,
        IClock clock,
        IOptions<ContactCenterRetentionOptions> options,
        ILogger<ContactCenterRetentionService> logger)
    {
        _policies = policies;
        _session = session;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterRetentionReport> PurgeAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = _clock.UtcNow;
        var batchSize = _options.PurgeBatchSize > 0 ? _options.PurgeBatchSize : ContactCenterRetentionOptions.DefaultPurgeBatchSize;
        var batchBudget = _options.MaxPurgeBatchesPerCycle > 0
            ? _options.MaxPurgeBatchesPerCycle
            : ContactCenterRetentionOptions.DefaultMaxPurgeBatchesPerCycle;

        var report = new ContactCenterRetentionReport();

        foreach (var policy in _policies)
        {
            // The budget is per entity. Sharing one budget across every entity would let whichever policy is
            // registered first consume all of it, and the tables behind it would never be purged at all.
            var remainingBatches = batchBudget;

            var result = new ContactCenterEntityRetentionResult
            {
                EntityName = policy.EntityName,
            };

            report.Entities.Add(result);

            if (!policy.TryGetCutoff(nowUtc, _options, out var cutoffUtc))
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
                catch (ContactCenterRetentionBatchException ex)
                {
                    // One entity failing must not stop the remaining entities from draining, otherwise a single
                    // unhealthy table would keep every other table growing forever. The failed batch staged a mix of
                    // completed records and the failing record's partial side effects into the shared session, and the
                    // session cannot commit only the completed records. Committing here would flush the failing
                    // record's partial state too — for example a recording-erased event whose outbox message was never
                    // staged, which the next cycle's idempotency key would then suppress, orphaning the media. Discard
                    // the whole batch instead so nothing partial is ever committed: every record it touched rolls back
                    // and is retried cleanly on the next cycle, and repeated deletes are idempotent.
                    _logger.LogError(ex, "Contact Center retention failed while purging entity {EntityName}; the batch was discarded and will be retried on the next cycle.", policy.EntityName);
                    result.WorkRemains = true;

                    try
                    {
                        await _session.ResetAsync();
                    }
                    catch (Exception resetException)
                    {
                        // A session that cannot even be reset is unusable, so stop the cycle rather than let the next
                        // entity run against a poisoned session and appear to purge while its deletes are discarded.
                        _logger.LogError(resetException, "Contact Center retention could not reset the session after a failed batch for entity {EntityName}. The cycle was stopped because the session cannot be reused.", policy.EntityName);
                        MarkUnvisitedEntitiesAsUnfinished(report, nowUtc);

                        return report;
                    }

                    break;
                }
                catch (Exception ex)
                {
                    // Reaching here means the batch failed before it staged any work — the only uncaught path left in
                    // a batch is the query that reads the expired records, and the per-record loop converts every
                    // later failure (a delete or a prepare side effect) into a ContactCenterRetentionBatchException.
                    // The session is therefore still usable, so the remaining entities can drain normally.
                    _logger.LogError(ex, "Contact Center retention failed while reading expired records for entity {EntityName}.", policy.EntityName);
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
                    await _session.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    // The deletes only reach the database here, so this is where a deadlock or a lost connection
                    // surfaces. A failed flush cancels the session permanently, which means every later entity
                    // would appear to purge while its deletes were silently discarded. Stop the cycle instead.
                    _logger.LogError(ex, "Contact Center retention failed while committing purged records for entity {EntityName}. The cycle was stopped because the session cannot be reused.", policy.EntityName);
                    result.WorkRemains = true;

                    // Entities the cycle never reached still have work. Leaving them out of the report would make
                    // an untouched backlog indistinguishable from a drained one in the operator warning.
                    MarkUnvisitedEntitiesAsUnfinished(report, nowUtc);

                    return report;
                }

                // Counted only once the deletes have actually committed, so a failed flush cannot over-report.
                result.PurgedCount += purged;

                if (purged < batchSize)
                {
                    break;
                }
            }
        }

        return report;
    }

    private void MarkUnvisitedEntitiesAsUnfinished(ContactCenterRetentionReport report, DateTime nowUtc)
    {
        var visited = report.Entities.Select(entity => entity.EntityName).ToHashSet(StringComparer.Ordinal);

        foreach (var policy in _policies)
        {
            if (visited.Contains(policy.EntityName))
            {
                continue;
            }

            // An entity whose window is disabled purges nothing, so reporting it as unfinished would name a table
            // in the operator warning that no amount of budget or window tuning could ever drain.
            var isEnabled = policy.TryGetCutoff(nowUtc, _options, out var cutoffUtc);

            report.Entities.Add(new ContactCenterEntityRetentionResult
            {
                EntityName = policy.EntityName,
                IsEnabled = isEnabled,
                CutoffUtc = isEnabled ? cutoffUtc : null,
                WorkRemains = isEnabled,
            });
        }
    }
}
