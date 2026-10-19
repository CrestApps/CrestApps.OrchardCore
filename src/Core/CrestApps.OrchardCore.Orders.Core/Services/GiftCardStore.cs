using CrestApps.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Persists gift cards.
/// </summary>
public interface IGiftCardStore : ICatalog<GiftCard>
{
    /// <summary>
    /// Returns the card with the code, typed with or without dashes, spaces or capitals.
    /// </summary>
    Task<GiftCard> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the cards an order bought.
    /// </summary>
    Task<IReadOnlyList<GiftCard>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of cards, newest first, and how many match.
    /// </summary>
    Task<(int Count, IReadOnlyList<GiftCard> Cards)> PageAsync(GiftCardQuery query, int skip, int take, CancellationToken cancellationToken = default);
}

/// <summary>
/// What gift cards to list.
/// </summary>
public sealed class GiftCardQuery
{
    /// <summary>
    /// Gets or sets a full code, its last four characters, or part of the recipient's email.
    /// </summary>
    public string Search { get; set; }

    public GiftCardStatusFilter Status { get; set; }
}

public enum GiftCardStatusFilter
{
    All,
    Active,
    UsedUp,
    Expired,
    Disabled,
}

/// <summary>
/// The default YesSql-backed <see cref="IGiftCardStore"/>.
/// </summary>
public sealed class GiftCardStore : DocumentCatalog<GiftCard, GiftCardIndex>, IGiftCardStore
{
    private readonly IClock _clock;

    // Two checkouts spending one card must not both write a stale balance.
    protected override bool CheckConcurrency => true;

    public GiftCardStore(ISession session, IClock clock)
        : base(session)
    {
        CollectionName = OrderConstants.CollectionName;
        _clock = clock;
    }

    /// <inheritdoc/>
    public Task<GiftCard> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = GiftCardCodes.Normalize(code);

        if (string.IsNullOrEmpty(normalized))
        {
            return Task.FromResult<GiftCard>(null);
        }

        return Session.Query<GiftCard, GiftCardIndex>(index => index.NormalizedCode == normalized, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GiftCard>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(orderId)
            ? []
            : [.. await Session.Query<GiftCard, GiftCardIndex>(index => index.OrderId == orderId, collection: CollectionName).ListAsync(cancellationToken)];

    /// <inheritdoc/>
    public async Task<(int Count, IReadOnlyList<GiftCard> Cards)> PageAsync(GiftCardQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        query ??= new GiftCardQuery();

        var cards = Session.Query<GiftCard, GiftCardIndex>(collection: CollectionName);
        var now = _clock.UtcNow;

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            var normalized = GiftCardCodes.Normalize(search);
            var email = search.ToLowerInvariant();

            cards = cards.Where(index => index.NormalizedCode == normalized || index.LastCharacters == normalized || index.RecipientEmail.Contains(email));
        }

        cards = query.Status switch
        {
            GiftCardStatusFilter.Active => cards.Where(index => index.IsEnabled && index.Balance > 0m && (index.ExpiresUtc == null || index.ExpiresUtc > now)),
            GiftCardStatusFilter.UsedUp => cards.Where(index => index.Balance <= 0m),
            GiftCardStatusFilter.Expired => cards.Where(index => index.ExpiresUtc != null && index.ExpiresUtc <= now),
            GiftCardStatusFilter.Disabled => cards.Where(index => !index.IsEnabled),
            _ => cards,
        };

        var count = await cards.CountAsync(cancellationToken);
        var page = await cards.OrderByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, [.. page]);
    }

    /// <inheritdoc/>
    protected override ValueTask SavingAsync(GiftCard record)
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
