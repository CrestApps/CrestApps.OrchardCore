using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using System.Security.Cryptography;
using System.Text.Json;
using CrestApps.Core.Services;
using OrchardCore;
using OrchardCore.Modules;
using YesSql.Services;
using YesSql;
using CrestApps.OrchardCore.Checkout.Core.Indexes;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default YesSql-backed <see cref="ICouponStore"/>.
/// </summary>
public sealed class CouponStore : DocumentCatalog<Coupon, CouponIndex>, ICouponStore
{
    private const int CodeLength = 8;

    // No 0, O, 1 or I, which people misread on a card.
    private static readonly char[] _codeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

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

        return FindAsync(normalized, cancellationToken);
    }

    private async Task<Coupon> FindAsync(string normalized, CancellationToken cancellationToken)
    {
        var coupon = await Session
            .Query<Coupon, CouponIndex>(x => x.NormalizedCode == normalized, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

        if (coupon is not null)
        {
            return coupon;
        }

        // A generated code is its coupon, with its own code, uses and limit.
        var generated = await Session
            .Query<CouponCode, CouponCodeIndex>(x => x.NormalizedCode == normalized, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

        if (generated is null)
        {
            return null;
        }

        var parent = await FindByIdAsync(generated.CouponId, cancellationToken);

        return parent is null ? null : ForGeneratedCode(parent, generated);
    }

    internal static Coupon ForGeneratedCode(Coupon parent, CouponCode generated)
    {
        var coupon = JsonSerializer.Deserialize<Coupon>(JsonSerializer.Serialize(parent));

        coupon.Id = 0;
        coupon.ItemId = generated.ItemId;
        coupon.Code = generated.Code;
        coupon.MaxRedemptions = generated.MaxRedemptions;
        coupon.RedemptionCount = generated.RedemptionCount;
        coupon.IsEnabled = parent.IsEnabled && generated.IsEnabled;
        coupon.CreatedUtc = generated.CreatedUtc;
        coupon.GeneratedCodeId = generated.ItemId;

        return coupon;
    }

    // A coupon looked up by a generated code saves its uses to that code, never over the coupon it came from.
    async ValueTask ICatalog<Coupon>.UpdateAsync(Coupon record, CancellationToken cancellationToken)
    {
        if (record?.GeneratedCodeId is null)
        {
            await UpdateAsync(record, cancellationToken);

            return;
        }

        var generated = await Session
            .Query<CouponCode, CouponCodeIndex>(x => x.ItemId == record.GeneratedCodeId, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

        if (generated is null)
        {
            return;
        }

        generated.RedemptionCount = record.RedemptionCount;

        await Session.SaveAsync(generated, checkConcurrency: true, collection: CollectionName, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CouponCode>> GenerateCodesAsync(Coupon coupon, int count, string prefix, int? usesPerCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coupon);

        count = Math.Clamp(count, 0, CheckoutConstants.MaxGeneratedCodes);
        prefix = prefix?.Trim().ToUpperInvariant() ?? string.Empty;

        var codes = new HashSet<string>(StringComparer.Ordinal);

        // Codes are random enough that clashes are rare; the few there are, with each other or with codes already in
        // use, are drawn again.
        for (var attempt = 0; attempt < 5 && codes.Count < count; attempt++)
        {
            var candidates = new HashSet<string>(StringComparer.Ordinal);

            while (candidates.Count < count - codes.Count)
            {
                var code = prefix + new string(RandomNumberGenerator.GetItems<char>(_codeAlphabet, CodeLength));

                if (!codes.Contains(code))
                {
                    candidates.Add(code);
                }
            }

            foreach (var chunk in candidates.Chunk(500))
            {
                var taken = (await Session.QueryIndex<CouponIndex>(x => x.NormalizedCode.IsIn(chunk), collection: CollectionName).ListAsync(cancellationToken))
                    .Select(index => index.NormalizedCode)
                    .Concat((await Session.QueryIndex<CouponCodeIndex>(x => x.NormalizedCode.IsIn(chunk), collection: CollectionName).ListAsync(cancellationToken))
                        .Select(index => index.NormalizedCode))
                    .ToHashSet(StringComparer.Ordinal);

                codes.UnionWith(chunk.Where(code => !taken.Contains(code)));
            }
        }

        var now = _clock.UtcNow;
        var generated = new List<CouponCode>();

        foreach (var code in codes.Take(count))
        {
            var entry = new CouponCode
            {
                ItemId = IdGenerator.GenerateId(),
                CouponId = coupon.ItemId,
                Code = code,
                MaxRedemptions = usesPerCode,
                CreatedUtc = now,
            };

            await Session.SaveAsync(entry, collection: CollectionName, cancellationToken: cancellationToken);
            generated.Add(entry);
        }

        return generated;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CouponCode>> ListGeneratedCodesAsync(string couponId, CancellationToken cancellationToken = default)
        => [.. await Session
            .Query<CouponCode, CouponCodeIndex>(x => x.CouponId == couponId, collection: CollectionName)
            .OrderBy(x => x.CreatedUtc)
            .ThenBy(x => x.NormalizedCode)
            .ListAsync(cancellationToken)];

    /// <inheritdoc/>
    public async Task<(int Total, int Used)> CountGeneratedCodesAsync(string couponId, CancellationToken cancellationToken = default)
    {
        var total = await Session.QueryIndex<CouponCodeIndex>(x => x.CouponId == couponId, collection: CollectionName).CountAsync(cancellationToken);

        if (total == 0)
        {
            return (0, 0);
        }

        var used = await Session.QueryIndex<CouponCodeIndex>(x => x.CouponId == couponId && x.RedemptionCount > 0, collection: CollectionName).CountAsync(cancellationToken);

        return (total, used);
    }

    /// <inheritdoc/>
    public async Task DeleteGeneratedCodesAsync(string couponId, CancellationToken cancellationToken = default)
    {
        foreach (var code in await ListGeneratedCodesAsync(couponId, cancellationToken))
        {
            Session.Delete(code, CollectionName);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Coupon>> ListAutomaticAsync(CancellationToken cancellationToken = default)
        => [.. await Session
            .Query<Coupon, CouponIndex>(x => x.IsAutomatic && x.IsEnabled, collection: CollectionName)
            .ListAsync(cancellationToken)];

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
