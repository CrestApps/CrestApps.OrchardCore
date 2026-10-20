using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Customers.Handlers;

/// <summary>
/// Puts the referral the buyer came from on each checkout they start, so the order it makes knows who referred them.
/// </summary>
internal sealed class ReferralCheckoutHandler : CheckoutHandlerBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ReferralCheckoutHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task ActivatingAsync(CheckoutFlowActivatingContext context)
    {
        var code = _httpContextAccessor.HttpContext?.Request.Cookies[ReferralService.CookieName];

        if (!string.IsNullOrWhiteSpace(code) && code.Length <= 16 && !context.Session.SettlesExistingBalance)
        {
            context.Session.Put(new CheckoutReferral { Code = code.Trim() });
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Rewards the customer who referred a buyer when the buyer's first order is placed, and takes the reward back when
/// that order is canceled.
/// </summary>
internal sealed class ReferralOrderEventHandler : OrderEventHandlerBase
{
    private readonly IReferralStore _store;
    private readonly ICustomerStore _customerStore;
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    // The order services raise the events this handles, so they're resolved when an event comes, not injected.
    public ReferralOrderEventHandler(
        IReferralStore store,
        ICustomerStore customerStore,
        ISiteService siteService,
        IServiceProvider serviceProvider,
        IClock clock,
        IStringLocalizer<ReferralOrderEventHandler> stringLocalizer)
    {
        _store = store;
        _customerStore = customerStore;
        _siteService = siteService;
        _serviceProvider = serviceProvider;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
    {
        var orderId = context.Order.ItemId;

        // The referrer is rewarded once the order is saved, in a scope of their own: a reward changes a balance under
        // its own lock and commits, which inside a checkout's completion would commit the checkout halfway.
        ShellScope.AddDeferredTask(scope => scope.ServiceProvider
            .GetServices<IOrderEventHandler>()
            .OfType<ReferralOrderEventHandler>()
            .First()
            .RewardReferrerAsync(orderId));

        return Task.CompletedTask;
    }

    internal async Task RewardReferrerAsync(string orderId, CancellationToken cancellationToken = default)
    {
        if (await _serviceProvider.GetRequiredService<IOrderStore>().FindByIdAsync(orderId, cancellationToken) is not { Status: OrderStatus.Paid } order)
        {
            return;
        }

        var friendEmail = order.CustomerEmail?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(friendEmail) || string.IsNullOrEmpty(order.CheckoutSessionId) ||
            await _serviceProvider.GetRequiredService<ICheckoutSessionStore>().GetAsync(order.CheckoutSessionId, cancellationToken) is not { } session ||
            !session.TryGet<CheckoutReferral>(out var referral) ||
            await _store.FindByCodeAsync(referral.Code, cancellationToken) is not { } profile ||
            await _customerStore.FindByIdAsync(profile.CustomerId, cancellationToken) is not { } referrer)
        {
            return;
        }

        // Nobody is rewarded for referring themselves, nor twice for one friend, nor for a friend who had bought
        // before.
        if (string.Equals(referrer.Email, friendEmail, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(order.UserId) && string.Equals(referrer.UserId, order.UserId, StringComparison.Ordinal)) ||
            profile.Referrals.Any(record => record.FriendEmail == friendEmail) ||
            await HasBoughtBeforeAsync(order, friendEmail, cancellationToken))
        {
            return;
        }

        var settings = await _siteService.GetSettingsAsync<ReferralSettings>();
        var goods = order.Subtotal - (order.Discounts ?? []).Where(discount => discount.Target != Checkout.Models.DiscountTarget.Shipping).Sum(discount => discount.Amount);

        if (goods < settings.MinimumOrderAmount)
        {
            return;
        }

        // The referrer sees a friend by their first name only.
        var friendName = string.IsNullOrWhiteSpace(order.CustomerName) ? friendEmail.Split('@')[0] : order.CustomerName.Trim().Split(' ')[0];
        var record = new ReferralRecord
        {
            FriendEmail = friendEmail,
            FriendName = friendName,
            OrderId = order.ItemId,
            OrderNumber = order.OrderNumber,
            CreatedUtc = _clock.UtcNow,
        };

        record.Reward = await RewardAsync(referrer, settings, order, record, S["Referred {0}, who placed order {1}", friendName, order.OrderNumber], cancellationToken);
        profile.Referrals.Add(record);

        var reward = record.Reward;

        await _store.UpdateAsync(profile, cancellationToken);
        await _serviceProvider.GetRequiredService<IOrderService>().AddNoteAsync(
            order,
            reward is null ? S["Referred by {0}.", referrer.GetName()] : S["Referred by {0}, who was given {1}.", referrer.GetName(), reward],
            OrderNoteKind.Event,
            null,
            cancellationToken);
    }

    public override async Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default)
    {
        var order = context.Order;

        if (string.IsNullOrEmpty(order.CheckoutSessionId) ||
            await _serviceProvider.GetRequiredService<ICheckoutSessionStore>().GetAsync(order.CheckoutSessionId, cancellationToken) is not { } session ||
            !session.TryGet<CheckoutReferral>(out var referral) ||
            await _store.FindByCodeAsync(referral.Code, cancellationToken) is not { } profile ||
            profile.Referrals.FirstOrDefault(record => record.OrderId == order.ItemId) is not { } record ||
            await _customerStore.FindByIdAsync(profile.CustomerId, cancellationToken) is not { } referrer)
        {
            return;
        }

        // The friend's order didn't happen, so neither does the reward; the friend can be referred again.
        var reason = S["Order {0}, which {1} was referred for, was canceled", order.OrderNumber, record.FriendName].Value;

        if (record.RewardKind == ReferralRewardKind.StoreCredit && record.RewardAmount > 0m && _serviceProvider.GetService<StoreCreditService>() is { } storeCredit)
        {
            await storeCredit.ChangeAsync(referrer.ItemId, record.RewardCurrency ?? order.Currency, -record.RewardAmount, reason, actor: null, cancellationToken: cancellationToken);
        }
        else if (record.RewardKind == ReferralRewardKind.Points && record.RewardPoints > 0 && _serviceProvider.GetService<LoyaltyService>() is { } loyalty)
        {
            await loyalty.ChangeAsync(referrer.ItemId, -record.RewardPoints, LoyaltyActivityKind.Reversed, reason, cancellationToken: cancellationToken);
        }

        profile.Referrals.Remove(record);
        await _store.UpdateAsync(profile, cancellationToken);
    }

    // Returns what the referrer was given, or null when the store gives nothing, or can't: the reward's feature is off.
    private async Task<string> RewardAsync(Customer referrer, ReferralSettings settings, Order order, ReferralRecord record, string reason, CancellationToken cancellationToken)
    {
        switch (settings.RewardKind)
        {
            case ReferralRewardKind.StoreCredit when settings.RewardAmount > 0m && !string.IsNullOrEmpty(referrer.UserId) && _serviceProvider.GetService<StoreCreditService>() is { } storeCredit:
                if (await storeCredit.ChangeAsync(referrer.ItemId, order.Currency, settings.RewardAmount, reason, actor: null, cancellationToken: cancellationToken) is null)
                {
                    return null;
                }

                record.RewardKind = ReferralRewardKind.StoreCredit;
                record.RewardAmount = settings.RewardAmount;
                record.RewardCurrency = order.Currency;

                return S["{0} store credit", ProductPriceFormatter.Format(settings.RewardAmount, order.Currency)].Value;

            case ReferralRewardKind.Points when settings.RewardPoints > 0 && !string.IsNullOrEmpty(referrer.UserId) && _serviceProvider.GetService<LoyaltyService>() is { } loyalty:
                if (await loyalty.ChangeAsync(referrer.ItemId, settings.RewardPoints, LoyaltyActivityKind.Adjusted, reason, cancellationToken: cancellationToken) is null)
                {
                    return null;
                }

                record.RewardKind = ReferralRewardKind.Points;
                record.RewardPoints = settings.RewardPoints;

                return S["{0:N0} points", settings.RewardPoints].Value;

            default:
                return null;
        }
    }

    private async Task<bool> HasBoughtBeforeAsync(Order order, string email, CancellationToken cancellationToken)
    {
        var orders = _serviceProvider.GetRequiredService<IOrderStore>();
        var (count, _) = await orders.PageAsync(new OrderQuery
        {
            UserId = string.IsNullOrEmpty(order.UserId) ? null : order.UserId,
            CustomerEmail = string.IsNullOrEmpty(order.UserId) ? email : null,
            Statuses = [OrderStatus.Paid],
        }, 0, 2, cancellationToken);

        return count > 1;
    }
}
