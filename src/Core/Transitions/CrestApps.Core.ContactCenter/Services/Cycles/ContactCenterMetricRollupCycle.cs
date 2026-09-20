using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Folds appended event count contributions into the daily totals they belong to. Counting is appended rather
/// than accumulated in place so that concurrent writers never contend for one row, which leaves the folding to
/// be done afterwards by a single roller. The background task lock is what makes it single-writer across
/// nodes, so the totals are only ever updated from one place at a time.
/// </summary>
public sealed class ContactCenterMetricRollupCycle : IContactCenterMetricRollupCycle
{
    private readonly IContactCenterMetricRollupService _rollupService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterMetricRollupCycle"/> class.
    /// </summary>
    /// <param name="rollupService">The rollup service.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterMetricRollupCycle(
        IContactCenterMetricRollupService rollupService,
        ILogger<ContactCenterMetricRollupCycle> logger)
    {
        _rollupService = rollupService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var folded = await _rollupService.RollupAsync(cancellationToken);

            if (folded > 0 && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Folded {Count} Contact Center event metric contribution(s) into their daily totals.", folded);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while folding Contact Center event metric contributions.");
        }
    }
}
