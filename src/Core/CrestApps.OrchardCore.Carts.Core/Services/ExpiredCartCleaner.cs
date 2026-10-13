using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Carts.Core.Services;

/// <summary>
/// Deletes carts nobody has touched within their lifetime.
/// </summary>
public sealed class ExpiredCartCleaner
{
    private const int BatchSize = 100;

    private readonly ICartStore _store;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public ExpiredCartCleaner(
        ICartStore store,
        ISession session,
        IClock clock,
        ILogger<ExpiredCartCleaner> logger)
    {
        _store = store;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Deletes up to <paramref name="maxBatches"/> batches of expired carts and returns how many were deleted.
    /// </summary>
    public async Task<int> DeleteExpiredAsync(int maxBatches = 10, CancellationToken cancellationToken = default)
    {
        var deleted = 0;

        for (var batch = 0; batch < maxBatches; batch++)
        {
            var carts = await _store.ListExpiredAsync(_clock.UtcNow, BatchSize, cancellationToken);

            if (carts.Count == 0)
            {
                break;
            }

            foreach (var cart in carts)
            {
                await _store.DeleteAsync(cart, cancellationToken);
            }

            // Each batch is committed before the next is read, so the next query does not return it again.
            await _session.SaveChangesAsync(cancellationToken);

            deleted += carts.Count;

            if (carts.Count < BatchSize)
            {
                break;
            }
        }

        if (deleted > 0 && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Deleted {Count} expired carts.", deleted);
        }

        return deleted;
    }
}
