using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Indexes;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// Persists customers' loyalty points.
/// </summary>
public interface ILoyaltyStore : ICatalog<LoyaltyAccount>
{
    Task<LoyaltyAccount> FindByCustomerAsync(string customerId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The default YesSql-backed <see cref="ILoyaltyStore"/>.
/// </summary>
public sealed class LoyaltyStore : DocumentCatalog<LoyaltyAccount, LoyaltyAccountIndex>, ILoyaltyStore
{
    private readonly IClock _clock;

    protected override bool CheckConcurrency => true;

    public LoyaltyStore(ISession session, IClock clock)
        : base(session)
    {
        CollectionName = CustomersConstants.CollectionName;
        _clock = clock;
    }

    public Task<LoyaltyAccount> FindByCustomerAsync(string customerId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(customerId)
            ? Task.FromResult<LoyaltyAccount>(null)
            : Session.Query<LoyaltyAccount, LoyaltyAccountIndex>(index => index.CustomerId == customerId, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);

    protected override ValueTask SavingAsync(LoyaltyAccount record)
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

/// <summary>
/// Gives and takes loyalty points, under the lock the checkout takes when it spends them.
/// </summary>
public sealed class LoyaltyService
{
    /// <summary>
    /// The key points pay toward checkouts with (<see cref="ICheckoutTenderProvider.Key"/>).
    /// </summary>
    public const string TenderKey = "loyalty-points";

    private readonly ILoyaltyStore _store;
    private readonly ISession _session;
    private readonly IDistributedLock _distributedLock;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public LoyaltyService(
        ILoyaltyStore store,
        ISession session,
        IDistributedLock distributedLock,
        ISiteService siteService,
        IClock clock,
        IStringLocalizer<LoyaltyService> stringLocalizer)
    {
        _store = store;
        _session = session;
        _distributedLock = distributedLock;
        _siteService = siteService;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns the program's settings, with its name filled in.
    /// </summary>
    public async Task<LoyaltySettings> GetSettingsAsync()
    {
        var settings = await _siteService.GetSettingsAsync<LoyaltySettings>();

        if (string.IsNullOrWhiteSpace(settings.ProgramName))
        {
            settings.ProgramName = S["Rewards"];
        }

        return settings;
    }

    /// <summary>
    /// Returns the points an order earns: its goods after discounts, before shipping and tax.
    /// </summary>
    public static int GetPointsFor(Order order, LoyaltySettings settings)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(settings);

        // A shipping discount came off shipping, which earns nothing, not off the goods; and what points paid earns no
        // more points.
        var basis = order.Subtotal -
            (order.Discounts ?? []).Where(discount => discount.Target != Checkout.Models.DiscountTarget.Shipping).Sum(discount => discount.Amount) -
            (order.Tenders ?? []).Where(tender => tender.ProviderKey == TenderKey).Sum(tender => tender.Amount);

        return basis <= 0m || settings.PointsPerUnit <= 0m ? 0 : (int)Math.Floor(basis * settings.PointsPerUnit);
    }

    /// <summary>
    /// Returns what <paramref name="points"/> are worth, to the cent below.
    /// </summary>
    public static decimal GetValue(int points, LoyaltySettings settings, string currency)
    {
        if (settings.PointValue <= 0m)
        {
            return 0m;
        }

        var factor = (decimal)Math.Pow(10, Payments.CurrencyScale.GetDecimalPlaces(currency));

        return Math.Floor(points * settings.PointValue * factor) / factor;
    }

    /// <summary>
    /// Returns how many points pay <paramref name="amount"/>, rounded up.
    /// </summary>
    public static int GetPointsWorth(decimal amount, LoyaltySettings settings)
        => settings.PointValue <= 0m ? 0 : (int)Math.Ceiling(amount / settings.PointValue);

    /// <summary>
    /// Changes the customer's points under their lock. Returns the account, or <see langword="null"/> when the lock
    /// couldn't be taken or there's nothing to take points from.
    /// </summary>
    public async Task<LoyaltyAccount> ChangeAsync(string customerId, int points, LoyaltyActivityKind kind, string reason, string actor = null, string orderId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(customerId);

        var account = await _store.FindByCustomerAsync(customerId, cancellationToken);

        if (account is null)
        {
            if (points <= 0)
            {
                return null;
            }

            // A first change makes the account, under the customer's lock, so two at once make one account.
            var (creating, created) = await _distributedLock.TryAcquireLockAsync(CheckoutTenderLocks.GetKey(TenderKey, "new-" + customerId), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));

            if (!created)
            {
                return null;
            }

            await using (creating)
            {
                account = await _store.FindByCustomerAsync(customerId, cancellationToken);

                if (account is null)
                {
                    account = new LoyaltyAccount { ItemId = IdGenerator.GenerateId(), CustomerId = customerId };
                    account.ChangePoints(points, kind, reason, _clock.UtcNow, orderId, actor: actor);

                    await _store.CreateAsync(account, cancellationToken);
                    await _session.SaveChangesAsync(cancellationToken);

                    return account;
                }
            }
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(CheckoutTenderLocks.GetKey(TenderKey, account.ItemId), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));

        if (!locked)
        {
            return null;
        }

        await using (locker)
        {
            account = await _store.FindByIdAsync(account.ItemId, cancellationToken);
            account.ChangePoints(points, kind, reason, _clock.UtcNow, orderId, actor: actor);

            await _store.UpdateAsync(account, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            return account;
        }
    }
}
