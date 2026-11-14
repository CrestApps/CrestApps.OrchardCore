using CrestApps.Core;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.AspNetCore.DataProtection;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Gives every worker whose feed secret was stored before secrets were hashed its hash and its protected copy. The plain
/// secret is kept for one release, so the previous release still opens the feed if it is rolled back. It reads and
/// commits a page of workers at a time, and does nothing to a worker that has its hash, so it can run again: from the
/// portal's migration, and once more when workers move to the Staff collection, which also upgrades each worker it moves.
/// </summary>
public sealed class WorkerFeedTokenBackfill
{
    private readonly ISession _session;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerFeedTokenBackfill"/> class.
    /// </summary>
    public WorkerFeedTokenBackfill(ISession session, IDataProtectionProvider dataProtectionProvider)
    {
        _session = session;
        _dataProtectionProvider = dataProtectionProvider;
    }

    /// <summary>
    /// Gets or sets how many workers are read and committed at a time.
    /// </summary>
    internal int BatchSize { get; set; } = 100;

    /// <summary>
    /// Runs the backfill.
    /// </summary>
    /// <returns>How many workers changed.</returns>
    public async Task<int> RunAsync()
    {
        var changed = 0;

        // Every worker with a feed keeps its index row (the hash of a plain secret is the hash it gets), so the pages
        // don't shift as workers are upgraded.
        for (var skip = 0; ; skip += BatchSize)
        {
            var workers = (await _session.Query<StaffMember, WorkerFeedIndex>(collection: StaffConstants.CollectionName)
                .OrderBy(index => index.WorkerId)
                .Skip(skip)
                .Take(BatchSize)
                .ListAsync()).ToList();

            foreach (var worker in workers)
            {
                if (Upgrade(worker, _dataProtectionProvider))
                {
                    await _session.SaveAsync(worker, false, StaffConstants.CollectionName);
                    changed++;
                }
            }

            await _session.SaveChangesAsync();

            if (workers.Count < BatchSize)
            {
                return changed;
            }
        }
    }

    /// <summary>
    /// Gives a worker whose feed secret is stored in plain text its hash and its protected copy.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="dataProtectionProvider">The tenant's data protection provider.</param>
    /// <returns>Whether the worker changed and needs saving.</returns>
    public static bool Upgrade(StaffMember worker, IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        if (!worker.TryGet<WorkerPortalSettings>(out var settings) ||
            string.IsNullOrEmpty(settings.FeedToken) ||
            !string.IsNullOrEmpty(settings.FeedTokenHash))
        {
            return false;
        }

        settings.FeedTokenHash = PortalSecrets.Hash(settings.FeedToken);
        settings.ProtectedFeedToken = dataProtectionProvider.CreateProtector(DefaultWorkerFeedService.ProtectionPurpose).Protect(settings.FeedToken);
        worker.Put(settings);

        return true;
    }
}
