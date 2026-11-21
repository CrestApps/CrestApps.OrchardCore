using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Throttling;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Checkout.Core.Handlers;

/// <summary>
/// The card-testing protection's gate. Before a provider that takes a card online is asked to charge it, it stops a
/// network or a signed-in customer whose declines reached the limit, a checkout that has begun too many payments, and,
/// during a store-wide run of declines, guests as the store chose. Always on: it needs no rate limit feature.
/// </summary>
public sealed class PaymentVelocityGate : ICheckoutPaymentGate
{
    private readonly IAttemptLimiter _limiter;
    private readonly ICheckoutPaymentProviderResolver _providerResolver;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentVelocityGate"/> class.
    /// </summary>
    public PaymentVelocityGate(
        IAttemptLimiter limiter,
        ICheckoutPaymentProviderResolver providerResolver,
        ISiteService siteService,
        IStringLocalizer<PaymentVelocityGate> stringLocalizer)
    {
        _limiter = limiter;
        _providerResolver = providerResolver;
        _siteService = siteService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task BeginningAsync(CheckoutPaymentBeginningContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrEmpty(context.ProviderKey) || _providerResolver.GetProvider(context.ProviderKey)?.Capabilities?.IsCardNotPresent != true)
        {
            return;
        }

        var session = context.Flow.Session;
        var networkKey = session.TryGet<CheckoutClientInfo>(out var client) ? client.NetworkKey : null;
        var ownerId = session.OwnerId;

        if (!string.IsNullOrEmpty(networkKey) &&
            !(await _limiter.PeekAsync(CheckoutConstants.AttemptPolicies.PaymentDeclineNetwork, networkKey, cancellationToken)).Allowed)
        {
            context.Refuse(S["We can't take another payment attempt right now. Please try again later or use another way to pay."]);

            return;
        }

        if (!string.IsNullOrEmpty(ownerId) &&
            !(await _limiter.PeekAsync(CheckoutConstants.AttemptPolicies.PaymentDeclineCustomer, ownerId, cancellationToken)).Allowed)
        {
            context.Refuse(S["We can't take another payment attempt right now. Please try again later or use another way to pay."]);

            return;
        }

        if (!(await _limiter.AcquireAsync(CheckoutConstants.AttemptPolicies.PaymentBeginCheckout, session.SessionId, cancellationToken)).Allowed)
        {
            context.Refuse(S["We can't take another payment attempt right now. Please try again later or use another way to pay."]);

            return;
        }

        // A store-wide run of declines is testers rotating networks, so no single network is stopped. Guests are who the
        // store's choice applies to: a signed-in customer has a limit of their own.
        if (!string.IsNullOrEmpty(ownerId) ||
            (await _limiter.PeekAsync(CheckoutConstants.AttemptPolicies.PaymentDeclineStore, PaymentDeclineRecorder.StoreKey, cancellationToken)).Allowed)
        {
            return;
        }

        var settings = (await _siteService.GetSiteSettingsAsync()).GetOrCreate<PaymentProtectionSettings>();

        switch (settings.OnDeclineSpike)
        {
            case DeclineSpikeAction.RequireSignIn:
                context.Refuse(S["Please sign in to pay by card, or use another way to pay."]);
                break;

            case DeclineSpikeAction.PauseGuestCards:
                context.Refuse(S["Card payments are paused for a little while. Please use another way to pay, or try again later."]);
                break;
        }
    }
}
