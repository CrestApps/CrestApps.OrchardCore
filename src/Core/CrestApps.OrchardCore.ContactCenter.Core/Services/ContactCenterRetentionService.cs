using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;
using CrestApps.OrchardCore.Retention;
using CrestApps.OrchardCore.YesSql.Core.Retention;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterRetentionService"/>. It runs the registered
/// Contact Center retention policies through the platform <see cref="RetentionRunner"/> with the windows, floors and
/// batch budget of <see cref="ContactCenterRetentionOptions"/>, draining each entity and reporting honestly when the
/// cycle budget stopped it short instead of truncating silently.
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
        var batchSize = _options.PurgeBatchSize > 0 ? _options.PurgeBatchSize : ContactCenterRetentionOptions.DefaultPurgeBatchSize;
        var batchBudget = _options.MaxPurgeBatchesPerCycle > 0
            ? _options.MaxPurgeBatchesPerCycle
            : ContactCenterRetentionOptions.DefaultMaxPurgeBatchesPerCycle;

        var result = await RetentionRunner.RunAsync(
            _policies.Select(policy => new OptionsBoundPolicy(policy, _options)).ToList(),
            _session,
            _clock.UtcNow,
            batchSize,
            batchBudget,
            _logger,
            cancellationToken);

        var report = new ContactCenterRetentionReport();

        foreach (var entity in result.Policies)
        {
            report.Entities.Add(new ContactCenterEntityRetentionResult
            {
                EntityName = entity.Name,
                IsEnabled = entity.IsEnabled,
                CutoffUtc = entity.CutoffUtc,
                PurgedCount = entity.PurgedCount,
                WorkRemains = entity.WorkRemains,
            });
        }

        return report;
    }

    // Runs a Contact Center policy as a platform policy: its cutoff comes from the Contact Center options, and its
    // failed batches are reported the way the platform runner discards them.
    private sealed class OptionsBoundPolicy : IRetentionPolicy
    {
        private readonly IContactCenterRetentionPolicy _policy;
        private readonly ContactCenterRetentionOptions _options;

        public OptionsBoundPolicy(IContactCenterRetentionPolicy policy, ContactCenterRetentionOptions options)
        {
            _policy = policy;
            _options = options;
        }

        public string Name => _policy.EntityName;

        public bool TryGetCutoff(DateTime nowUtc, out DateTime cutoffUtc)
            => _policy.TryGetCutoff(nowUtc, _options, out cutoffUtc);

        public async Task<int> PurgeBatchAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _policy.PurgeBatchAsync(cutoffUtc, batchSize, cancellationToken);
            }
            catch (ContactCenterRetentionBatchException ex)
            {
                throw new RetentionBatchException(_policy.EntityName, 0, ex);
            }
        }
    }
}
