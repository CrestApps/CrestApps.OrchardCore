using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default YesSql-backed <see cref="ICouponStore"/>.
/// </summary>
public sealed class CouponStore : DocumentCatalog<Coupon, CouponIndex>, ICouponStore
{
    private readonly IClock _clock;

    /// <summary>
    /// Enables document-version concurrency checks so two purchases redeeming the same coupon at once
    /// cannot both write a stale count and give away an extra redemption.
    /// </summary>
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouponStore"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session.</param>
    /// <param name="clock">The clock used for timestamps.</param>
    public CouponStore(ISession session, IClock clock)
        : base(session)
    {
        CollectionName = CheckoutConstants.CouponCollectionName;
        _clock = clock;
    }

    /// <inheritdoc/>
    public Task<Coupon> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult<Coupon>(null);
        }

        // Matching on a normalized column rather than relying on the database's collation means a code
        // behaves the same on SQLite, SQL Server, and PostgreSQL.
        var normalized = code.Trim().ToUpperInvariant();

        return Session
            .Query<Coupon, CouponIndex>(x => x.NormalizedCode == normalized, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    protected override ValueTask SavingAsync(Coupon record)
    {
        var now = _clock.UtcNow;

        if (record.CreatedUtc == default)
        {
            record.CreatedUtc = now;
        }

        record.UpdatedUtc = now;

        return ValueTask.CompletedTask;
    }
}
