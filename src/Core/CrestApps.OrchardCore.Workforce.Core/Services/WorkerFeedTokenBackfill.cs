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
/// secret is kept for one release, so the previous release still opens the feed if it is rolled back.
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
    /// Runs the backfill.
    /// </summary>
    /// <returns>How many workers changed.</returns>
    public async Task<int> RunAsync()
    {
        var changed = 0;
        var workers = await _session.Query<StaffMember, WorkerFeedIndex>(collection: StaffConstants.CollectionName).ListAsync();

        foreach (var worker in workers)
        {
            if (!worker.TryGet<WorkerPortalSettings>(out var settings) ||
                string.IsNullOrEmpty(settings.FeedToken) ||
                !string.IsNullOrEmpty(settings.FeedTokenHash))
            {
                continue;
            }

            settings.FeedTokenHash = PortalSecrets.Hash(settings.FeedToken);
            settings.ProtectedFeedToken = _dataProtectionProvider.CreateProtector(DefaultWorkerFeedService.ProtectionPurpose).Protect(settings.FeedToken);
            worker.Put(settings);

            await _session.SaveAsync(worker, false, StaffConstants.CollectionName);
            changed++;
        }

        await _session.SaveChangesAsync();

        return changed;
    }
}
