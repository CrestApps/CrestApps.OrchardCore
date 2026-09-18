using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Drains every Contact Center table of records beyond its configured data-governance retention window once a
/// day.
/// </summary>
public sealed class ContactCenterRetentionCycle : IContactCenterRetentionCycle
{
    private readonly IContactCenterRetentionService _retentionService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterRetentionCycle"/> class.
    /// </summary>
    /// <param name="retentionService">The retention service.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterRetentionCycle(
        IContactCenterRetentionService retentionService,
        ILogger<ContactCenterRetentionCycle> logger)
    {
        _retentionService = retentionService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var report = await _retentionService.PurgeAsync(cancellationToken);

            if (report.WorkRemains)
            {
                // Without this the cycle would look successful while the database kept growing, which is the
                // failure mode a silently truncating batch cap produces.
                var starvedEntities = string.Join(
                    ", ",
                    report.Entities.Where(entity => entity.WorkRemains).Select(entity => entity.EntityName));

                _logger.LogWarning(
                    "Contact Center retention purged {PurgedCount} records but did not reach steady state for: {StarvedEntities}. Raise 'CrestApps:ContactCenter:Retention:MaxPurgeBatchesPerCycle' or shorten the retention windows.",
                    report.TotalPurged,
                    starvedEntities);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while purging expired Contact Center records.");
        }
    }
}
