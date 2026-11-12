using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Commerce.Stores;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Entities;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// The agent checkout over the store's own checkout. What the agent asks for is kept on an <see cref="AgentCheckout"/>
/// and applied to a store checkout through the same services the storefront's pages use, so an agent sees the store's
/// prices, tax, shipping, discounts and stock holds, and pays through the checkout engine.
/// </summary>
public sealed class AgentCheckoutService : IAgentCheckoutService
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);

    private readonly IAgentCheckoutStore _store;
    private readonly ICheckoutSessionStore _sessions;
    private readonly ICheckoutEngine _engine;
    private readonly ICartService _carts;
    private readonly IOrderService _orders;
    private readonly IOrderStore _orderStore;
    private readonly IEnumerable<IOrderLinkService> _orderLinks;
    private readonly IEnumerable<CheckoutCouponService> _coupons;
    private readonly IEnumerable<ICheckoutContactStep> _contactSteps;
    private readonly IEnumerable<ICheckoutFulfillmentStep> _fulfillmentSteps;
    private readonly IEnumerable<IAgentPaymentHandler> _paymentHandlers;
    private readonly ICheckoutPaymentProviderResolver _providers;
    private readonly IAgentItemResolver _items;
    private readonly ISiteService _siteService;
    private readonly AgentSiteUrls _urls;
    private readonly IDistributedLock _locks;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentCheckoutService"/> class.
    /// </summary>
    /// <param name="store">The agent checkout store.</param>
    /// <param name="sessions">The store checkout store.</param>
    /// <param name="engine">The checkout engine.</param>
    /// <param name="carts">The cart service, which prices the agent's items.</param>
    /// <param name="orders">The order service.</param>
    /// <param name="orderStore">The order store.</param>
    /// <param name="orderLinks">The order link service, when the Orders module offers one.</param>
    /// <param name="coupons">The discount code service, when the Checkout module offers one.</param>
    /// <param name="contactSteps">The Contact step, when a module offers one.</param>
    /// <param name="fulfillmentSteps">The Shipping step, when a module offers one.</param>
    /// <param name="paymentHandlers">The payment handlers.</param>
    /// <param name="providers">The checkout payment providers, for what they can do.</param>
    /// <param name="items">The item id resolver.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="urls">The agent addresses.</param>
    /// <param name="locks">The distributed lock.</param>
    /// <param name="session">The YesSql session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AgentCheckoutService(
        IAgentCheckoutStore store,
        ICheckoutSessionStore sessions,
        ICheckoutEngine engine,
        ICartService carts,
        IOrderService orders,
        IOrderStore orderStore,
        IEnumerable<IOrderLinkService> orderLinks,
        IEnumerable<CheckoutCouponService> coupons,
        IEnumerable<ICheckoutContactStep> contactSteps,
        IEnumerable<ICheckoutFulfillmentStep> fulfillmentSteps,
        IEnumerable<IAgentPaymentHandler> paymentHandlers,
        ICheckoutPaymentProviderResolver providers,
        IAgentItemResolver items,
        ISiteService siteService,
        AgentSiteUrls urls,
        IDistributedLock locks,
        ISession session,
        IClock clock,
        ILogger<AgentCheckoutService> logger,
        IStringLocalizer<AgentCheckoutService> stringLocalizer)
    {
        _store = store;
        _sessions = sessions;
        _engine = engine;
        _carts = carts;
        _orders = orders;
        _orderStore = orderStore;
        _orderLinks = orderLinks;
        _coupons = coupons;
        _contactSteps = contactSteps;
        _fulfillmentSteps = fulfillmentSteps;
        _paymentHandlers = paymentHandlers;
        _providers = providers;
        _items = items;
        _siteService = siteService;
        _urls = urls;
        _locks = locks;
        _session = session;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task<AgentCheckoutResult> CreateAsync(AgentCheckoutContext context, AgentCheckoutInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);

        var lines = Normalize(input.Lines);

        if (lines.Count == 0)
        {
            return AgentCheckoutResult.Fail(AgentCheckoutFailure.Invalid, S["Give at least one item, with a quantity of one or more."]);
        }

        var settings = await _siteService.GetSettingsAsync<AgenticCommerceSettings>();

        if (await _store.CountOpenAsync(context.PlatformId, cancellationToken) >= Math.Max(1, settings.MaxOpenCheckoutsPerPlatform))
        {
            return AgentCheckoutResult.Fail(AgentCheckoutFailure.TooMany, S["Too many checkouts are open. Complete or cancel some first."]);
        }

        var now = _clock.UtcNow;
        var checkout = new AgentCheckout
        {
            ItemId = IdGenerator.GenerateId(),
            PlatformId = context.PlatformId,
            Protocol = context.Protocol,
            ProtocolVersion = context.ProtocolVersion,
            UserId = context.UserId,
            Status = AgentCheckoutStatus.Incomplete,
            Lines = lines,
            Buyer = input.Buyer,
            Fulfillment = input.Fulfillment,
            DiscountCodes = NormalizeCodes(input.DiscountCodes),
            BillingAddress = input.BillingAddress,
            Attribution = input.Attribution,
            Signals = input.Signals is null ? [] : new Dictionary<string, string>(input.Signals),
            Consents = input.Consents is null ? [] : new Dictionary<string, bool>(input.Consents),
            OrderNotes = input.OrderNotes,
            CreatedUtc = now,
            ModifiedUtc = now,
            ExpiresUtc = now.AddHours(Math.Clamp(settings.CheckoutLifetimeHours, 1, 72)),
        };

        foreach (var (name, value) in context.Properties ?? new Dictionary<string, string>())
        {
            checkout.SetProperty(name, value);
        }

        if (context.RunsThreeDSecure)
        {
            checkout.SetProperty(DelegatedAuthentication.AgentRunsThreeDSecureProperty, "true");
        }

        var view = await SyncAsync(checkout, context, cancellationToken);

        await _store.CreateAsync(checkout, cancellationToken);

        return new AgentCheckoutResult
        {
            Checkout = checkout,
            View = view,
        };
    }

    /// <inheritdoc />
    public Task<AgentCheckoutResult> GetAsync(AgentCheckoutContext context, string id, CancellationToken cancellationToken = default)
        => WithCheckoutAsync(context, id, async checkout =>
        {
            await RefreshAsync(checkout, cancellationToken);

            return new AgentCheckoutResult
            {
                Checkout = checkout,
                View = await SyncAsync(checkout, context, cancellationToken),
            };
        }, cancellationToken);

    /// <inheritdoc />
    public Task<AgentCheckoutResult> UpdateAsync(AgentCheckoutContext context, string id, AgentCheckoutUpdate update, CancellationToken cancellationToken = default)
        => WithCheckoutAsync(context, id, async checkout =>
        {
            ArgumentNullException.ThrowIfNull(update);

            await RefreshAsync(checkout, cancellationToken);

            if (checkout.IsTerminal || await IsBeingPaidAsync(checkout, cancellationToken))
            {
                return AgentCheckoutResult.Fail(
                    AgentCheckoutFailure.InvalidState,
                    S["The checkout can't be changed any more."],
                    checkout,
                    await SyncAsync(checkout, context, cancellationToken));
            }

            if (update.Lines is not null && Normalize(update.Lines).Count == 0)
            {
                return AgentCheckoutResult.Fail(AgentCheckoutFailure.Invalid, S["Give at least one item, with a quantity of one or more."], checkout);
            }

            Apply(checkout, update);

            return new AgentCheckoutResult
            {
                Checkout = checkout,
                View = await SyncAsync(checkout, context, cancellationToken),
            };
        }, cancellationToken);

    /// <inheritdoc />
    public Task<AgentCheckoutResult> CompleteAsync(AgentCheckoutContext context, string id, AgentPayment payment, AgentCheckoutUpdate update, CancellationToken cancellationToken = default)
        => WithCheckoutAsync(context, id, async checkout =>
        {
            ArgumentNullException.ThrowIfNull(payment);

            await RefreshAsync(checkout, cancellationToken);

            if (checkout.IsTerminal)
            {
                return AgentCheckoutResult.Fail(
                    AgentCheckoutFailure.InvalidState,
                    checkout.Status == AgentCheckoutStatus.Completed ? S["The checkout is already completed."] : S["The checkout can't be completed any more."],
                    checkout,
                    await SyncAsync(checkout, context, cancellationToken));
            }

            // The bank asked the buyer to approve the payment and the agent ran 3-D Secure: this request finishes that
            // payment with the result, and changes nothing else.
            if (checkout.Authentication is not null)
            {
                return await ContinueAuthenticationAsync(checkout, context, payment, cancellationToken);
            }

            if (update is not null && !await IsBeingPaidAsync(checkout, cancellationToken))
            {
                Apply(checkout, update);
            }

            if (payment.BillingAddress is not null)
            {
                checkout.BillingAddress = payment.BillingAddress;
            }

            var view = await SyncAsync(checkout, context, cancellationToken);

            if (view.Status != AgentCheckoutStatus.ReadyForComplete)
            {
                return new AgentCheckoutResult
                {
                    Checkout = checkout,
                    View = view,
                };
            }

            await PayAsync(checkout, context, view, payment, cancellationToken);

            return new AgentCheckoutResult
            {
                Checkout = checkout,
                View = await SyncAsync(checkout, context, cancellationToken),
            };
        }, cancellationToken);

    // Finishes a payment the bank asked the buyer to approve, with the result of the 3-D Secure the agent ran. The same
    // payment attempt is resumed, so the provider confirms the payment it began rather than making a second one.
    private async Task<AgentCheckoutResult> ContinueAuthenticationAsync(AgentCheckout checkout, AgentCheckoutContext context, AgentPayment payment, CancellationToken cancellationToken)
    {
        var result = payment.Authentication;

        switch (DelegatedAuthentication.Classify(checkout.Authentication, result))
        {
            case DelegatedAuthenticationStep.Missing:
                return AgentCheckoutResult.Fail(
                    AgentCheckoutFailure.AuthenticationRequired,
                    S["The buyer's bank asks for 3-D Secure. Authenticate the buyer, then complete the checkout with the result."],
                    checkout,
                    await SyncAsync(checkout, context, cancellationToken));

            case DelegatedAuthenticationStep.Invalid:
                return AgentCheckoutResult.Fail(
                    AgentCheckoutFailure.Invalid,
                    S["The 3-D Secure result isn't complete or valid: check its {0}.", result.GetInvalidMember()],
                    checkout,
                    await SyncAsync(checkout, context, cancellationToken));

            case DelegatedAuthenticationStep.NotAuthenticated:
                // The payment can't go ahead. Its store checkout is canceled, which voids what the provider began, and
                // the next request starts a new one the agent may pay another way.
                if (!string.IsNullOrEmpty(checkout.CheckoutSessionId))
                {
                    await _engine.CancelAsync(checkout.CheckoutSessionId, "The buyer's bank didn't authenticate them.", cancellationToken);
                }

                checkout.Authentication = null;
                checkout.PaymentMessage = null;
                checkout.ModifiedUtc = _clock.UtcNow;

                var view = await SyncAsync(checkout, context, cancellationToken);

                checkout.PaymentMessage = AgentCheckoutMessage.Error(
                    AgentMessageCodes.PaymentDeclined,
                    AgentCheckoutFields.Payment,
                    S["The buyer's bank didn't authenticate them ({0}), so the payment wasn't taken. Pay another way, or try again.", result.Outcome]);
                view.Messages.Add(checkout.PaymentMessage);

                return new AgentCheckoutResult
                {
                    Checkout = checkout,
                    View = view,
                };
        }

        await PayAsync(checkout, context, await SyncAsync(checkout, context, cancellationToken), payment, cancellationToken);

        return new AgentCheckoutResult
        {
            Checkout = checkout,
            View = await SyncAsync(checkout, context, cancellationToken),
        };
    }

    /// <inheritdoc />
    public Task<AgentCheckoutResult> CancelAsync(AgentCheckoutContext context, string id, JsonObject intentTrace, CancellationToken cancellationToken = default)
        => WithCheckoutAsync(context, id, async checkout =>
        {
            await RefreshAsync(checkout, cancellationToken);

            if (checkout.IsTerminal || await IsBeingPaidAsync(checkout, cancellationToken))
            {
                return AgentCheckoutResult.Fail(
                    AgentCheckoutFailure.InvalidState,
                    S["The checkout can't be canceled any more."],
                    checkout,
                    await SyncAsync(checkout, context, cancellationToken));
            }

            if (!string.IsNullOrEmpty(checkout.CheckoutSessionId))
            {
                await _engine.CancelAsync(checkout.CheckoutSessionId, "Canceled by the agent.", cancellationToken);
            }

            checkout.Status = AgentCheckoutStatus.Canceled;
            checkout.IntentTrace = intentTrace;
            checkout.Signals = [];
            checkout.ModifiedUtc = _clock.UtcNow;

            return new AgentCheckoutResult
            {
                Checkout = checkout,
                View = await SyncAsync(checkout, context, cancellationToken),
            };
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<AgentCheckoutView> PreviewAsync(AgentCheckoutContext context, IList<AgentCheckoutLineInput> lines, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var now = _clock.UtcNow;
        var preview = new AgentCheckout
        {
            ItemId = IdGenerator.GenerateId(),
            PlatformId = context.PlatformId,
            Protocol = context.Protocol,
            UserId = context.UserId,
            Lines = Normalize(lines),
            CreatedUtc = now,
            ExpiresUtc = now,
        };

        var messages = new List<AgentCheckoutMessage>();
        var pricing = await PriceAsync(preview, messages, cancellationToken);
        var view = await BuildViewAsync(preview, session: null, messages, fulfillment: null, pricing, cancellationToken);

        view.ContinueUrl = null;
        view.ExpiresUtc = null;

        return view;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentPaymentHandlerDescriptor>> GetPaymentHandlersAsync(AgentCheckoutContext context, AgentCheckoutView view, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var handlerContext = CreateHandlerContext(context, view);
        var descriptors = new List<AgentPaymentHandlerDescriptor>();

        foreach (var handler in _paymentHandlers)
        {
            var descriptor = await handler.DescribeAsync(handlerContext, cancellationToken);

            if (descriptor is null)
            {
                continue;
            }

            if (context.AllowedPaymentHandlers?.Count > 0 && !context.AllowedPaymentHandlers.Contains(descriptor.Name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            descriptors.Add(descriptor);
        }

        return descriptors;
    }

    /// <inheritdoc />
    public async Task<int> ExpireAsync(CancellationToken cancellationToken = default)
    {
        var expired = 0;

        foreach (var candidate in await _store.ListExpiredAsync(_clock.UtcNow, 50, cancellationToken))
        {
            var result = await WithCheckoutAsync(context: null, candidate.ItemId, async checkout =>
            {
                await RefreshAsync(checkout, cancellationToken);

                return new AgentCheckoutResult { Checkout = checkout };
            }, cancellationToken);

            if (result.Checkout?.Status == AgentCheckoutStatus.Expired)
            {
                expired++;
            }
        }

        return expired;
    }

    // Requests for the same checkout are taken one at a time, so two agents' retries never make two store checkouts.
    private async Task<AgentCheckoutResult> WithCheckoutAsync(AgentCheckoutContext context, string id, Func<AgentCheckout, Task<AgentCheckoutResult>> action, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(id))
        {
            return AgentCheckoutResult.Fail(AgentCheckoutFailure.NotFound, S["There is no such checkout."]);
        }

        var (locker, locked) = await _locks.TryAcquireLockAsync($"agent-checkout:{id}", _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return AgentCheckoutResult.Fail(AgentCheckoutFailure.Busy, S["Another request is changing this checkout. Try again in a moment."]);
        }

        await using (locker)
        {
            var checkout = await _store.FindByIdAsync(id, cancellationToken);

            // A platform only sees its own checkouts; the store's background work sees all of them. A cart is not a
            // checkout: taking its id here would make it a store checkout (with stock held) outside the platform's cap.
            if (checkout is null ||
                (context is not null && !string.Equals(checkout.PlatformId, context.PlatformId, StringComparison.Ordinal)) ||
                (context is not null && string.Equals(checkout.GetProperty(AgentCheckout.KindProperty), AgentCheckout.CartKind, StringComparison.Ordinal)))
            {
                return AgentCheckoutResult.Fail(AgentCheckoutFailure.NotFound, S["There is no such checkout."]);
            }

            var result = await action(checkout);

            if (result.Checkout is not null)
            {
                await _store.UpdateAsync(await GetTrackedAsync(result.Checkout, cancellationToken), cancellationToken);

                // Committed before the lock is released, so the next request for this checkout reads what this one
                // did (a completed checkout, a payment in progress) and never pays again from a stale copy.
                await _session.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }

    // The checkout engine commits the session part way through a request (it must, so a payment it starts is on record
    // before the provider is called). A commit makes YesSql forget the documents it loaded, so saving the copy this
    // request changed would insert a second row with the same id, and a later read could find the stale one: a paid
    // checkout that looks payable again. The changes go onto the row as it's stored now instead.
    private async Task<AgentCheckout> GetTrackedAsync(AgentCheckout changed, CancellationToken cancellationToken)
    {
        var tracked = await _store.FindByIdAsync(changed.ItemId, cancellationToken);

        if (tracked is null || ReferenceEquals(tracked, changed))
        {
            return changed;
        }

        foreach (var property in typeof(AgentCheckout).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
            {
                property.SetValue(tracked, property.GetValue(changed));
            }
        }

        return tracked;
    }

    // A checkout that ran out of time expires; one being paid moves on when the provider confirms the payment.
    private async Task RefreshAsync(AgentCheckout checkout, CancellationToken cancellationToken)
    {
        if (checkout.IsTerminal)
        {
            return;
        }

        var session = await GetSessionAsync(checkout, cancellationToken);

        if (session?.Status is CheckoutSessionStatus.AwaitingProvider or CheckoutSessionStatus.PaymentPending)
        {
            var completion = await _engine.TryCompleteAsync(session.SessionId, cancellationToken);

            if (completion.IsCompleted)
            {
                MarkCompleted(checkout);

                return;
            }

            if (completion.Status is CheckoutCompletionStatus.Failed or CheckoutCompletionStatus.Canceled)
            {
                checkout.PaymentMessage = AgentCheckoutMessage.Error(AgentMessageCodes.PaymentFailed, AgentCheckoutFields.Payment, completion.ErrorMessage ?? S["The payment failed."]);
                checkout.EscalationUrl = null;
                checkout.Authentication = null;
            }

            return;
        }

        if (session?.Status == CheckoutSessionStatus.Completed)
        {
            MarkCompleted(checkout);

            return;
        }

        if (checkout.ExpiresUtc < _clock.UtcNow)
        {
            if (session?.Status == CheckoutSessionStatus.Pending)
            {
                await _engine.ExpireAsync(session.SessionId, cancellationToken);
            }

            checkout.Status = AgentCheckoutStatus.Expired;
            checkout.Signals = [];
            checkout.ModifiedUtc = _clock.UtcNow;
        }
    }

    private async Task<bool> IsBeingPaidAsync(AgentCheckout checkout, CancellationToken cancellationToken)
        => (await GetSessionAsync(checkout, cancellationToken))?.Status is CheckoutSessionStatus.AwaitingProvider or CheckoutSessionStatus.PaymentPending;

    private async Task<CheckoutSession> GetSessionAsync(AgentCheckout checkout, CancellationToken cancellationToken)
        => string.IsNullOrEmpty(checkout.CheckoutSessionId) ? null : await _sessions.GetAsync(checkout.CheckoutSessionId, cancellationToken);

    private void Apply(AgentCheckout checkout, AgentCheckoutUpdate update)
    {
        if (update.Lines is not null)
        {
            checkout.Lines = Normalize(update.Lines);
        }

        if (update.Buyer is not null)
        {
            checkout.Buyer = update.Buyer;
        }

        if (update.ClearFulfillment)
        {
            checkout.Fulfillment = null;
        }
        else if (update.Fulfillment is not null)
        {
            checkout.Fulfillment = update.Fulfillment;
        }

        if (update.DiscountCodes is not null)
        {
            checkout.DiscountCodes = NormalizeCodes(update.DiscountCodes);
        }

        if (update.BillingAddress is not null)
        {
            checkout.BillingAddress = update.BillingAddress;
        }

        if (update.Attribution is not null)
        {
            checkout.Attribution = update.Attribution;
        }

        foreach (var (name, value) in update.Signals ?? new Dictionary<string, string>())
        {
            checkout.Signals[name] = value;
        }

        foreach (var (purpose, granted) in update.Consents ?? new Dictionary<string, bool>())
        {
            checkout.Consents[purpose] = granted;
        }

        if (update.OrderNotes is not null)
        {
            checkout.OrderNotes = update.OrderNotes;
        }

        // A new try starts clean: what went wrong with the last payment is about the old checkout.
        checkout.PaymentMessage = null;
        checkout.EscalationUrl = null;
        checkout.Authentication = null;
        checkout.ModifiedUtc = _clock.UtcNow;
    }

    private async Task PayAsync(AgentCheckout checkout, AgentCheckoutContext context, AgentCheckoutView view, AgentPayment payment, CancellationToken cancellationToken)
    {
        checkout.PaymentMessage = null;
        checkout.EscalationUrl = null;

        var handlerContext = CreateHandlerContext(context, view);
        IAgentPaymentHandler handler = null;
        AgentPaymentHandlerDescriptor descriptor = null;

        foreach (var candidate in _paymentHandlers)
        {
            var described = await candidate.DescribeAsync(handlerContext, cancellationToken);

            if (described is null ||
                (context.AllowedPaymentHandlers?.Count > 0 && !context.AllowedPaymentHandlers.Contains(described.Name, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (string.Equals(described.Id, payment.HandlerId, StringComparison.Ordinal) ||
                string.Equals(described.Name, payment.HandlerId, StringComparison.Ordinal))
            {
                handler = candidate;
                descriptor = described;

                break;
            }
        }

        if (handler is null)
        {
            checkout.PaymentMessage = AgentCheckoutMessage.Error(AgentMessageCodes.PaymentFailed, AgentCheckoutFields.Payment, S["The store doesn't take payments through that payment handler."]);

            return;
        }

        var preparation = await handler.PrepareAsync(handlerContext, payment, cancellationToken);

        if (!preparation.Succeeded)
        {
            checkout.PaymentMessage = AgentCheckoutMessage.Error(AgentMessageCodes.PaymentFailed, AgentCheckoutFields.Payment, preparation.ErrorMessage ?? S["The payment credential can't be used."]);

            return;
        }

        var pending = checkout.Authentication;

        // The result finishes the payment the provider began; paying through another provider would begin a second one.
        if (pending is not null && !string.Equals(pending.ProviderKey, preparation.ProviderKey, StringComparison.OrdinalIgnoreCase))
        {
            checkout.PaymentMessage = AgentCheckoutMessage.Error(AgentMessageCodes.PaymentFailed, AgentCheckoutFields.Payment, S["Complete the checkout with the payment handler the buyer authenticated."]);

            return;
        }

        var settings = await _siteService.GetSettingsAsync<AgenticCommerceSettings>();
        var provider = _providers.GetProvider(preparation.ProviderKey);
        var delegable = DelegatedAuthentication.IsConfigured(settings) &&
            DelegatedAuthentication.AgentRunsThreeDSecure(checkout) &&
            provider?.Capabilities?.AcceptsExternalAuthentication == true;

        var providerData = new Dictionary<string, string>(preparation.ProviderData ?? new Dictionary<string, string>());
        var sendsResult = DelegatedAuthentication.SendsResult(payment.Authentication, pending, delegable);

        if (sendsResult)
        {
            payment.Authentication.CopyTo(providerData);
        }

        checkout.Authentication = null;

        var handoffUrl = await _urls.GetHandoffUrlAsync(checkout);
        var outcome = await _engine.BeginPaymentAsync(checkout.CheckoutSessionId, new BeginPaymentOptions
        {
            ProviderKey = preparation.ProviderKey,
            ProviderData = providerData,
            ReturnUrl = handoffUrl,
            CancelUrl = handoffUrl,
        }, cancellationToken);

        checkout.PaymentDisplay = string.Join(' ', new[] { payment.Brand, payment.LastDigits }.Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!outcome.Succeeded)
        {
            if (pending is not null && !outcome.Declined && !outcome.InvoiceChanged)
            {
                // Nothing was decided, such as a provider that couldn't be reached: the agent may send the same result
                // again, which resumes the same attempt and replays at the provider.
                checkout.Authentication = pending;
                checkout.PaymentMessage = AgentCheckoutMessage.Error(AgentMessageCodes.PaymentFailed, AgentCheckoutFields.Payment, outcome.ErrorMessage ?? S["The payment couldn't be finished. Complete the checkout again with the same result."]);

                return;
            }

            if (pending is not null && !outcome.RequiresAuthentication && !string.IsNullOrEmpty(checkout.CheckoutSessionId))
            {
                // The payment the buyer authenticated can't go ahead: its store checkout is closed, which voids what the
                // provider began, and the next request starts a new one.
                await _engine.CancelAsync(checkout.CheckoutSessionId, "The payment didn't go ahead after 3-D Secure.", cancellationToken);
            }

            if (outcome.InvoiceChanged)
            {
                checkout.PaymentMessage = AgentCheckoutMessage.Error(AgentMessageCodes.PriceChanged, null, S["The total changed. Review the checkout and complete it again."], AgentMessageSeverity.RequiresBuyerReview);

                return;
            }

            if (outcome.RequiresAuthentication)
            {
                // An agent that runs 3-D Secure itself is asked to, once; a result that didn't satisfy the bank sends the
                // buyer to the store's checkout instead of asking again.
                if (!sendsResult && delegable && RequestAuthentication(checkout, preparation.ProviderKey, payment.Brand ?? outcome.CardBrand))
                {
                    return;
                }

                checkout.EscalationUrl = handoffUrl;
                checkout.PaymentMessage = AgentCheckoutMessage.Error(AgentMessageCodes.Requires3ds, AgentCheckoutFields.Payment, S["The buyer's bank asks them to approve the payment, in the store's checkout."], AgentMessageSeverity.RequiresBuyerInput);

                return;
            }

            checkout.PaymentMessage = AgentCheckoutMessage.Error(
                outcome.Declined ? AgentMessageCodes.PaymentDeclined : AgentMessageCodes.PaymentFailed,
                AgentCheckoutFields.Payment,
                outcome.ProviderErrorMessage ?? outcome.ErrorMessage ?? S["The payment failed."]);

            return;
        }

        if (outcome.RequiresAction)
        {
            // An agent that runs 3-D Secure itself authenticates the buyer and completes again with the result, which
            // the provider imports into this same payment.
            if (!sendsResult && delegable && RequestAuthentication(checkout, preparation.ProviderKey, payment.Brand ?? outcome.CardBrand))
            {
                return;
            }

            // The buyer's bank asks them to approve the payment: the agent hands the buyer the page it is on.
            checkout.EscalationUrl = outcome.Steps.FirstOrDefault(step => step.RequiresAction && !string.IsNullOrEmpty(step.RedirectUrl))?.RedirectUrl ?? handoffUrl;
            checkout.PaymentMessage = AgentCheckoutMessage.Error(AgentMessageCodes.Requires3ds, AgentCheckoutFields.Payment, S["The buyer's bank asks them to approve the payment."], AgentMessageSeverity.RequiresBuyerInput);

            return;
        }

        var completion = await _engine.TryCompleteAsync(checkout.CheckoutSessionId, cancellationToken);

        if (completion.IsCompleted)
        {
            MarkCompleted(checkout);

            return;
        }

        if (completion.Status is CheckoutCompletionStatus.Failed or CheckoutCompletionStatus.Canceled or CheckoutCompletionStatus.Blocked)
        {
            checkout.PaymentMessage = AgentCheckoutMessage.Error(AgentMessageCodes.PaymentFailed, AgentCheckoutFields.Payment, completion.ErrorMessage ?? S["The payment failed."]);
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Agent checkout '{CheckoutId}' paid through '{Handler}'; the checkout is {Status}.", checkout.ItemId, descriptor.Name, completion.Status);
        }
    }

    // Asks the agent to run 3-D Secure with the card network's directory server; false when the card's brand has none.
    private bool RequestAuthentication(AgentCheckout checkout, string providerKey, string cardBrand)
    {
        var directoryServer = AgentDirectoryServers.FromBrand(cardBrand);

        if (directoryServer is null)
        {
            return false;
        }

        checkout.Authentication = new AgentPendingAuthentication
        {
            ProviderKey = providerKey,
            DirectoryServer = directoryServer,
            RequestedUtc = _clock.UtcNow,
        };
        checkout.EscalationUrl = null;
        checkout.PaymentMessage = AgentCheckoutMessage.Error(
            AgentMessageCodes.Requires3ds,
            AgentCheckoutFields.Payment,
            S["The buyer's bank asks them to approve the payment with 3-D Secure. Authenticate the buyer, then complete the checkout with the result."],
            AgentMessageSeverity.RequiresBuyerInput);

        return true;
    }

    private void MarkCompleted(AgentCheckout checkout)
    {
        checkout.Status = AgentCheckoutStatus.Completed;
        checkout.CompletedUtc ??= _clock.UtcNow;
        checkout.ModifiedUtc = _clock.UtcNow;
        checkout.PaymentMessage = null;
        checkout.EscalationUrl = null;
        checkout.Authentication = null;

        // What the agent observed about the buyer was for the risk check; it isn't kept after the payment settles.
        checkout.Signals = [];
    }

    // Applies what the agent asked for to the store checkout, then returns where the checkout stands.
    private async Task<AgentCheckoutView> SyncAsync(AgentCheckout checkout, AgentCheckoutContext context, CancellationToken cancellationToken)
    {
        var session = await GetSessionAsync(checkout, cancellationToken);
        var messages = new List<AgentCheckoutMessage>();

        if (checkout.IsTerminal)
        {
            return await BuildViewAsync(checkout, session, messages, fulfillment: null, pricing: null, cancellationToken);
        }

        if (session?.Status is CheckoutSessionStatus.AwaitingProvider or CheckoutSessionStatus.PaymentPending)
        {
            return await BuildViewAsync(checkout, session, messages, fulfillment: null, pricing: null, cancellationToken);
        }

        var linesKey = Fingerprint(checkout.Lines);

        if (session is null || session.Status != CheckoutSessionStatus.Pending || !string.Equals(checkout.AppliedLines, linesKey, StringComparison.Ordinal))
        {
            var pricing = await PriceAsync(checkout, messages, cancellationToken);

            // An item the store doesn't sell, or can't sell as asked, keeps the whole checkout from starting.
            if (pricing is null || !pricing.CanCheckout || messages.Any(message => message.Type == AgentMessageType.Error))
            {
                await ReleaseAsync(checkout, session, cancellationToken);

                return await BuildViewAsync(checkout, session: null, messages, fulfillment: null, pricing, cancellationToken);
            }

            session = await StartAsync(checkout, session, pricing, messages, cancellationToken);

            if (session is null)
            {
                return await BuildViewAsync(checkout, session: null, messages, fulfillment: null, pricing, cancellationToken);
            }

            checkout.AppliedLines = linesKey;
        }

        var flow = new CheckoutFlow(session);

        await ApplyContactAsync(checkout, flow, messages, cancellationToken);

        var fulfillment = await ApplyFulfillmentAsync(checkout, flow, messages, cancellationToken);

        await ApplyCodesAsync(checkout, flow, context, messages);

        await _sessions.SaveAsync(session, cancellationToken);

        return await BuildViewAsync(checkout, session, messages, fulfillment, pricing: null, cancellationToken);
    }

    // The agent's items are priced as a cart the store keeps for this checkout only, so every rule of the storefront
    // applies: what can be bought, how many, at which price.
    private async Task<CartPricing> PriceAsync(AgentCheckout checkout, List<AgentCheckoutMessage> messages, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var cart = new Cart
        {
            ItemId = checkout.ItemId,
            UserId = checkout.UserId,
            Currency = checkout.Currency,
            CreatedUtc = now,
            ModifiedUtc = now,
            ExpiresUtc = checkout.ExpiresUtc,
        };

        var indexes = new List<int>();

        for (var i = 0; i < checkout.Lines.Count; i++)
        {
            var line = checkout.Lines[i];
            var item = await _items.ResolveAsync(line.ItemId, cancellationToken);

            if (item is null)
            {
                messages.Add(AgentCheckoutMessage.Error(AgentMessageCodes.NotFound, AgentCheckoutFields.LineItem(i), S["The store doesn't sell an item with the id '{0}'.", line.ItemId]));

                continue;
            }

            cart.Lines.Add(new CartLine
            {
                LineId = IdGenerator.GenerateId(),
                ContentItemId = item.ContentItemId,
                VariantId = item.VariantId,
                Quantity = line.Quantity,
                AddedUtc = now,
            });

            indexes.Add(i);
        }

        if (cart.Lines.Count == 0)
        {
            return null;
        }

        var pricing = await _carts.PriceAsync(cart, cancellationToken);

        for (var i = 0; i < pricing.Lines.Count; i++)
        {
            var line = pricing.Lines[i];

            if (!line.IsAvailable)
            {
                var index = i < indexes.Count ? indexes[i] : i;

                messages.Add(AgentCheckoutMessage.Error(
                    line.MaxQuantity <= 0 ? AgentMessageCodes.OutOfStock : AgentMessageCodes.ItemUnavailable,
                    AgentCheckoutFields.LineItem(index),
                    line.Problem?.Value ?? S["This item can't be bought as it is."]));
            }
        }

        checkout.Currency = pricing.Currency;

        return pricing;
    }

    private async Task<CheckoutSession> StartAsync(AgentCheckout checkout, CheckoutSession previous, CartPricing pricing, List<AgentCheckoutMessage> messages, CancellationToken cancellationToken)
    {
        // The store checkout of the old items is replaced: it releases what it held, and the new order cancels the old
        // one, as a buyer who changes their cart and checks out again does.
        await ReleaseAsync(checkout, previous, cancellationToken);

        var created = await _orders.CreateFromCartAsync(pricing, order =>
        {
            order.Channel = AgenticCommerceStorage.OrderChannel;
            order.UserId = checkout.UserId;
        }, cancellationToken);

        if (!created.Succeeded)
        {
            foreach (var problem in created.Problems)
            {
                messages.Add(AgentCheckoutMessage.Error(AgentMessageCodes.OutOfStock, null, problem));
            }

            return null;
        }

        try
        {
            // Committed with the stock the order holds, so a buyer who just took the last units elsewhere makes this one
            // fail cleanly instead of selling them twice.
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            await _session.CancelAsync();
            messages.Add(AgentCheckoutMessage.Error(AgentMessageCodes.OutOfStock, null, S["Someone else is buying the same items right now. Try again."]));

            return null;
        }

        var session = await _engine.StartAsync(new StartCheckoutRequest
        {
            ReferenceType = CheckoutReferenceTypes.Order,
            ReferenceId = created.Order.ItemId,
            OwnerId = checkout.UserId,
        }, cancellationToken);

        // The request runs as the agent platform, which the store checkout would otherwise take as its owner: it
        // belongs to the linked buyer, or to nobody until the hand-off link gives it to the buyer's browser.
        if (!string.Equals(session.OwnerId, checkout.UserId, StringComparison.Ordinal))
        {
            session.OwnerId = checkout.UserId;
            session.GuestTokenHash = null;
            await _sessions.SaveAsync(session, cancellationToken);
        }

        checkout.CheckoutSessionId = session.SessionId;
        checkout.OrderId = created.Order.ItemId;
        checkout.AppliedContact = null;
        checkout.AppliedFulfillment = null;

        return session;
    }

    private async Task ReleaseAsync(AgentCheckout checkout, CheckoutSession session, CancellationToken cancellationToken)
    {
        if (session?.Status == CheckoutSessionStatus.Pending)
        {
            await _engine.CancelAsync(session.SessionId, "The agent changed the items.", cancellationToken);
        }

        checkout.CheckoutSessionId = null;
        checkout.OrderId = null;
        checkout.AppliedLines = null;
        checkout.AppliedContact = null;
        checkout.AppliedFulfillment = null;
    }

    private async Task ApplyContactAsync(AgentCheckout checkout, CheckoutFlow flow, List<AgentCheckoutMessage> messages, CancellationToken cancellationToken)
    {
        var step = _contactSteps.FirstOrDefault();

        if (step is null || !HasStep(flow, step.StepKey) || string.IsNullOrWhiteSpace(checkout.Buyer?.Email))
        {
            return;
        }

        var shippingAddress = checkout.Fulfillment?.Address;
        var details = new CheckoutContactDetails
        {
            Name = checkout.Buyer.GetFullName() ?? shippingAddress?.Name ?? checkout.BillingAddress?.Name ?? checkout.Buyer.Email,
            Email = checkout.Buyer.Email,
            Phone = checkout.Buyer.Phone ?? shippingAddress?.Phone,

            // A buyer who gives no billing address is billed where the order ships.
            BillingAddress = checkout.BillingAddress ?? shippingAddress,
        };

        var key = Fingerprint(details);

        if (string.Equals(checkout.AppliedContact, key, StringComparison.Ordinal) && flow.Session.SavedSteps.ContainsKey(step.StepKey))
        {
            return;
        }

        var result = await step.SaveAsync(flow, details, cancellationToken);

        if (result.Saved)
        {
            checkout.AppliedContact = key;

            return;
        }

        checkout.AppliedContact = null;

        foreach (var error in result.Errors)
        {
            messages.Add(AgentCheckoutMessage.Error(AgentMessageCodes.Invalid, ToContactField(error.Field), error.Message, AgentMessageSeverity.RequiresBuyerInput));
        }
    }

    private async Task<AgentFulfillmentView> ApplyFulfillmentAsync(AgentCheckout checkout, CheckoutFlow flow, List<AgentCheckoutMessage> messages, CancellationToken cancellationToken)
    {
        var step = _fulfillmentSteps.FirstOrDefault();

        if (step is null || !HasStep(flow, step.StepKey))
        {
            return new AgentFulfillmentView { Required = false };
        }

        var view = new AgentFulfillmentView
        {
            Required = true,
            PickupLocations = (await step.GetPickupOptionsAsync(flow, cancellationToken))
                .Select(location => new AgentPickupLocationView
                {
                    Id = location.Id,
                    Name = location.Name,
                    Address = location.Address,
                    Fee = location.Fee,
                })
                .ToList(),
        };

        var fulfillment = checkout.Fulfillment;

        if (!string.IsNullOrEmpty(fulfillment?.PickupLocationId))
        {
            view.SelectedPickupLocationId = fulfillment.PickupLocationId;

            var pickupKey = "pickup:" + fulfillment.PickupLocationId;

            if (!string.Equals(checkout.AppliedFulfillment, pickupKey, StringComparison.Ordinal) || !flow.Session.SavedSteps.ContainsKey(step.StepKey))
            {
                var saved = await step.SaveAsync(flow, new CheckoutFulfillmentChoice { PickupLocationId = fulfillment.PickupLocationId }, cancellationToken);

                checkout.AppliedFulfillment = saved.Saved ? pickupKey : null;
                AddErrors(messages, saved, AgentCheckoutFields.FulfillmentPickup);
            }

            return view;
        }

        if (fulfillment?.Address is null)
        {
            return view;
        }

        var address = Complete(fulfillment.Address, checkout.Buyer);
        view.Address = address;

        var quote = await step.QuoteAsync(flow, address, cancellationToken);

        view.Options = quote.Options
            .Select(option => new AgentFulfillmentOptionView
            {
                Id = option.Id,
                Title = option.Title,
                Description = option.Description,
                Carrier = option.Carrier,
                Amount = option.Amount,
            })
            .ToList();

        if (view.Options.Count == 0)
        {
            messages.Add(AgentCheckoutMessage.Error(AgentMessageCodes.AddressUndeliverable, AgentCheckoutFields.FulfillmentAddress, quote.Problem ?? S["The store doesn't ship to this address."]));
            checkout.AppliedFulfillment = null;

            return view;
        }

        // Without a choice, the cheapest way ships; the agent may pick another from the options.
        var optionId = fulfillment.OptionId;

        if (string.IsNullOrEmpty(optionId) || !view.Options.Any(option => option.Id == optionId))
        {
            if (!string.IsNullOrEmpty(optionId))
            {
                messages.Add(AgentCheckoutMessage.Warning(AgentMessageCodes.Invalid, AgentCheckoutFields.FulfillmentOption, S["That way to ship isn't offered for this address; the cheapest one is chosen instead."]));
            }

            optionId = view.Options.OrderBy(option => option.Amount).First().Id;
        }

        view.SelectedOptionId = optionId;

        var key = Fingerprint(new { address, optionId });

        if (!string.Equals(checkout.AppliedFulfillment, key, StringComparison.Ordinal) || !flow.Session.SavedSteps.ContainsKey(step.StepKey))
        {
            var saved = await step.SaveAsync(flow, new CheckoutFulfillmentChoice
            {
                Address = address,
                OptionId = optionId,
            }, cancellationToken);

            checkout.AppliedFulfillment = saved.Saved ? key : null;
            AddErrors(messages, saved, AgentCheckoutFields.FulfillmentAddress);
        }

        return view;
    }

    private async Task ApplyCodesAsync(AgentCheckout checkout, CheckoutFlow flow, AgentCheckoutContext context, List<AgentCheckoutMessage> messages)
    {
        var coupons = _coupons.FirstOrDefault();

        if (coupons is null)
        {
            return;
        }

        var current = CheckoutCouponService.GetCodes(flow);
        var desired = checkout.DiscountCodes ?? [];

        foreach (var code in current.Where(code => !desired.Contains(code, StringComparer.OrdinalIgnoreCase)).ToList())
        {
            await coupons.RemoveAsync(flow, code);
        }

        for (var i = 0; i < desired.Count; i++)
        {
            if (current.Contains(desired[i], StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (await coupons.ApplyAsync(flow, desired[i], context?.ClientAddress ?? "agent") is { } error)
            {
                messages.Add(AgentCheckoutMessage.Warning(AgentMessageCodes.DiscountCodeInvalid, AgentCheckoutFields.DiscountCode(i), error));
            }
        }
    }

    private static AgentFulfillmentView WithDestinationId(AgentFulfillmentView fulfillment, string destinationId)
    {
        if (fulfillment is not null)
        {
            fulfillment.DestinationId = destinationId;
        }

        return fulfillment;
    }

    private async Task<AgentCheckoutView> BuildViewAsync(
        AgentCheckout checkout,
        CheckoutSession session,
        List<AgentCheckoutMessage> messages,
        AgentFulfillmentView fulfillment,
        CartPricing pricing,
        CancellationToken cancellationToken)
    {
        var invoice = session is not null && session.TryGet<CheckoutInvoice>(out var found) ? found : null;
        // UCP and ACP require a currency on every checkout, even one with nothing priced yet (an unknown or sold-out
        // item): it's then the store's checkout currency.
        var currency = invoice?.Currency ?? pricing?.Currency ?? checkout.Currency ?? session?.Currency
            ?? (await _siteService.GetSettingsAsync<CheckoutSettings>())?.Currency;
        var order = string.IsNullOrEmpty(checkout.OrderId) ? null : await _orderStore.FindByIdAsync(checkout.OrderId, cancellationToken);

        var view = new AgentCheckoutView
        {
            Id = checkout.ItemId,
            Protocol = checkout.Protocol,
            Currency = currency,
            Buyer = checkout.Buyer,
            Consents = checkout.Consents ?? [],
            BillingAddress = checkout.BillingAddress,
            DiscountCodes = checkout.DiscountCodes ?? [],
            ExpiresUtc = checkout.IsTerminal ? null : checkout.ExpiresUtc,
            Fulfillment = WithDestinationId(fulfillment ?? FulfillmentFromSession(checkout, session), checkout.Fulfillment?.DestinationId),
            Links = await BuildLinksAsync(),
        };

        if (order is not null)
        {
            foreach (var line in order.Lines)
            {
                var invoiceLine = invoice?.LineItems?.FirstOrDefault(item => item.ItemId == line.LineId);
                var unitPrice = invoiceLine?.UnitPrice ?? line.UnitPrice;
                var discount = invoiceLine?.DiscountAmount ?? 0;

                view.Lines.Add(new AgentCheckoutLineView
                {
                    Id = line.LineId,
                    ItemId = _items.GetItemId(line.ContentItemId, line.VariantId, line.Sku),
                    Title = string.IsNullOrEmpty(line.VariantTitle) ? line.Title : $"{line.Title} ({line.VariantTitle})",
                    Quantity = line.Quantity,
                    UnitPrice = unitPrice,
                    Subtotal = unitPrice * line.Quantity,
                    Discount = discount,
                    Total = (unitPrice * line.Quantity) - discount,
                });
            }

            // A store checkout that ended may no longer hold its invoice; the order still has what was asked.
            view.Totals = invoice is null
                ? new AgentCheckoutTotals
                {
                    Subtotal = order.Subtotal,
                    Discount = order.Discounts?.Sum(discount => discount.Amount) ?? 0,
                    Fulfillment = order.Shipping?.Amount ?? 0,
                    Tax = order.TaxAmount ?? 0,
                    Total = order.Total ?? order.Subtotal,
                }
                : BuildTotals(invoice);
            view.Discounts = (invoice?.Discounts ?? [])
                .Select(entry => new AgentAppliedDiscount
                {
                    Code = entry.Code,
                    Title = entry.Description,
                    Amount = entry.Amount,
                    Automatic = entry.IsAutomatic,
                })
                .ToList();
        }
        else if (pricing is not null)
        {
            foreach (var line in pricing.Lines)
            {
                view.Lines.Add(new AgentCheckoutLineView
                {
                    Id = line.Line.LineId,
                    ItemId = _items.GetItemId(line.Line.ContentItemId, line.Line.VariantId, line.Product?.Sku),
                    Title = line.Title,
                    Quantity = line.Line.Quantity,
                    UnitPrice = line.UnitPrice,
                    Subtotal = line.LineTotal,
                    Total = line.LineTotal,
                });
            }

            view.Totals = new AgentCheckoutTotals
            {
                Subtotal = pricing.Subtotal,
                Total = pricing.Subtotal,
            };
        }

        view.Status = ComputeStatus(checkout, session, messages, view);

        var settings = await _siteService.GetSettingsAsync<AgenticCommerceSettings>();

        view.AcceptsAuthenticationResult = !checkout.IsTerminal && DelegatedAuthentication.IsConfigured(settings) && DelegatedAuthentication.AgentRunsThreeDSecure(checkout);

        if (view.Status == AgentCheckoutStatus.AuthenticationRequired)
        {
            view.AuthenticationMetadata = DelegatedAuthentication.BuildMetadata(settings, checkout.Authentication.DirectoryServer);
        }

        foreach (var message in messages)
        {
            view.Messages.Add(message);
        }

        if (checkout.PaymentMessage is not null && !checkout.IsTerminal)
        {
            view.Messages.Add(checkout.PaymentMessage);
        }

        if (!checkout.IsTerminal)
        {
            view.ContinueUrl = view.Status == AgentCheckoutStatus.RequiresEscalation && !string.IsNullOrEmpty(checkout.EscalationUrl)
                ? checkout.EscalationUrl
                : await _urls.GetHandoffUrlAsync(checkout);
        }

        if (checkout.Status == AgentCheckoutStatus.Completed && order is not null)
        {
            var links = _orderLinks.FirstOrDefault();

            view.Order = new AgentOrderReference
            {
                Id = order.ItemId,
                Number = order.OrderNumber,
                PermalinkUrl = links is null ? await _urls.GetBaseUrlAsync() : await links.CreateUrlAsync(order),
            };
        }

        return view;
    }

    private AgentCheckoutStatus ComputeStatus(AgentCheckout checkout, CheckoutSession session, List<AgentCheckoutMessage> messages, AgentCheckoutView view)
    {
        if (checkout.IsTerminal)
        {
            return checkout.Status;
        }

        if (session is null)
        {
            return AgentCheckoutStatus.Incomplete;
        }

        // The agent runs 3-D Secure with the buyer, then completes again with the result.
        if (checkout.Authentication is not null)
        {
            return AgentCheckoutStatus.AuthenticationRequired;
        }

        if (session.Status is CheckoutSessionStatus.AwaitingProvider or CheckoutSessionStatus.PaymentPending)
        {
            return string.IsNullOrEmpty(checkout.EscalationUrl) ? AgentCheckoutStatus.CompleteInProgress : AgentCheckoutStatus.RequiresEscalation;
        }

        if (!string.IsNullOrEmpty(checkout.EscalationUrl))
        {
            return AgentCheckoutStatus.RequiresEscalation;
        }

        if (messages.Any(message => message.Type == AgentMessageType.Error))
        {
            return AgentCheckoutStatus.Incomplete;
        }

        var flow = new CheckoutFlow(session);
        var pending = flow.GetSortedSteps().FirstOrDefault(step =>
            !string.Equals(step.Key, CheckoutConstants.PaymentStepKey, StringComparison.Ordinal) &&
            step.CollectData &&
            !session.SavedSteps.ContainsKey(step.Key));

        if (pending is null)
        {
            return AgentCheckoutStatus.ReadyForComplete;
        }

        if (_contactSteps.FirstOrDefault()?.StepKey == pending.Key)
        {
            messages.Add(AgentCheckoutMessage.Error(AgentMessageCodes.Missing, AgentCheckoutFields.BuyerEmail, S["Give the buyer's e-mail address."], AgentMessageSeverity.RequiresBuyerInput));

            return AgentCheckoutStatus.Incomplete;
        }

        if (_fulfillmentSteps.FirstOrDefault()?.StepKey == pending.Key)
        {
            messages.Add(AgentCheckoutMessage.Error(AgentMessageCodes.Missing, AgentCheckoutFields.FulfillmentAddress, S["Give the address to ship to, or a place to collect from."], AgentMessageSeverity.RequiresBuyerInput));

            return AgentCheckoutStatus.Incomplete;
        }

        // A step an agent can't fill, such as the store's own checkout fields or a company's approval, is finished by the
        // buyer in the store's checkout.
        messages.Add(AgentCheckoutMessage.Error(AgentMessageCodes.RequiresBuyerInput, null, S["The store needs details the buyer gives in its checkout: {0}.", pending.Title ?? pending.Key], AgentMessageSeverity.RequiresBuyerInput));

        return AgentCheckoutStatus.RequiresEscalation;
    }

    // The parts add up to the total, as every protocol requires: what isn't subtotal, discount, shipping or tax added on
    // top is a fee, and tax already in the prices isn't added again.
    internal static AgentCheckoutTotals BuildTotals(CheckoutInvoice invoice)
    {
        var lines = invoice.LineItems ?? [];
        var subtotal = lines.Where(line => line.TaxableKind != TaxableItemKind.Shipping).Sum(line => line.UnitPrice * line.Quantity);
        var shipping = lines.Where(line => line.TaxableKind == TaxableItemKind.Shipping).Sum(line => line.UnitPrice * line.Quantity);

        return AgentCheckoutTotals.Balance(subtotal, invoice.DiscountTotal, shipping, invoice.TaxAmount, invoice.GrandTotal);
    }

    private static AgentFulfillmentView FulfillmentFromSession(AgentCheckout checkout, CheckoutSession session)
    {
        var view = new AgentFulfillmentView
        {
            Required = checkout.Fulfillment is not null,
            Address = checkout.Fulfillment?.Address,
            SelectedPickupLocationId = checkout.Fulfillment?.PickupLocationId,
        };

        if (session is not null && session.TryGet<CheckoutShippingInfo>(out var shipping) && !string.IsNullOrEmpty(shipping.RateId))
        {
            view.Required = true;
            view.Address = shipping.Address ?? view.Address;
            view.SelectedOptionId = shipping.RateId;
            view.Options.Add(new AgentFulfillmentOptionView
            {
                Id = shipping.RateId,
                Title = shipping.Name,
                Description = shipping.Description,
                Carrier = shipping.ProviderName,
                Amount = shipping.Amount,
            });
        }

        return view;
    }

    private async Task<IList<AgentLink>> BuildLinksAsync()
    {
        var profile = await _siteService.GetStoreProfileAsync();
        var policies = profile.Policies ?? new StorePolicyLinks();
        var links = new List<AgentLink>();

        foreach (var (type, link) in new[]
        {
            (AgentLinkTypes.TermsOfService, policies.TermsOfService),
            (AgentLinkTypes.PrivacyPolicy, policies.PrivacyPolicy),
            (AgentLinkTypes.ReturnPolicy, policies.ReturnPolicy),
            (AgentLinkTypes.ShippingPolicy, policies.ShippingPolicy),
            (AgentLinkTypes.Faq, policies.Faq),
            (AgentLinkTypes.Contact, policies.Contact),
            (AgentLinkTypes.About, policies.About),
        })
        {
            if (await _urls.ToAbsoluteAsync(link) is { } url)
            {
                links.Add(new AgentLink
                {
                    Type = type,
                    Url = url,
                });
            }
        }

        return links;
    }

    private static AgentPaymentHandlerContext CreateHandlerContext(AgentCheckoutContext context, AgentCheckoutView view)
        => new()
        {
            Protocol = context.Protocol,
            ProtocolVersion = context.ProtocolVersion,
            PlatformId = context.PlatformId,
            TestMode = context.TestMode,
            Currency = view?.Currency,
            Total = view?.Totals?.Total,
        };

    private static bool HasStep(CheckoutFlow flow, string key)
        => flow.Session.Steps?.Any(step => string.Equals(step.Key, key, StringComparison.Ordinal)) == true;

    // An address an agent gives may leave the recipient out; it is the buyer.
    private static Address Complete(Address address, AgentBuyer buyer)
        => new()
        {
            Name = string.IsNullOrWhiteSpace(address.Name) ? buyer?.GetFullName() : address.Name,
            Company = address.Company,
            AddressLine1 = address.AddressLine1,
            AddressLine2 = address.AddressLine2,
            DependentLocality = address.DependentLocality,
            City = address.City,
            Region = address.Region,
            PostalCode = address.PostalCode,
            SortingCode = address.SortingCode,
            Country = address.Country,
            Phone = string.IsNullOrWhiteSpace(address.Phone) ? buyer?.Phone : address.Phone,
        };

    private static void AddErrors(List<AgentCheckoutMessage> messages, CheckoutStepSaveResult result, string field)
    {
        if (result.Saved)
        {
            return;
        }

        if (result.Errors.Count == 0)
        {
            messages.Add(AgentCheckoutMessage.Error(AgentMessageCodes.Invalid, field, result.Choosing ? "Choose one of the options." : "This can't be saved as it is.", AgentMessageSeverity.RequiresBuyerInput));

            return;
        }

        foreach (var error in result.Errors)
        {
            var errorField = error.Field?.Contains("Rate", StringComparison.OrdinalIgnoreCase) == true ? AgentCheckoutFields.FulfillmentOption : field;

            messages.Add(AgentCheckoutMessage.Error(AgentMessageCodes.Invalid, errorField, error.Message, AgentMessageSeverity.RequiresBuyerInput));
        }
    }

    private static string ToContactField(string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return null;
        }

        if (field.StartsWith("Email", StringComparison.OrdinalIgnoreCase))
        {
            return AgentCheckoutFields.BuyerEmail;
        }

        if (field.StartsWith("Name", StringComparison.OrdinalIgnoreCase))
        {
            return AgentCheckoutFields.BuyerName;
        }

        if (field.StartsWith("Phone", StringComparison.OrdinalIgnoreCase))
        {
            return AgentCheckoutFields.BuyerPhone;
        }

        return field.StartsWith("Billing", StringComparison.OrdinalIgnoreCase) ? AgentCheckoutFields.BillingAddress : null;
    }

    internal static List<AgentCheckoutLineInput> Normalize(IEnumerable<AgentCheckoutLineInput> lines)
        => (lines ?? [])
            .Where(line => line is not null && !string.IsNullOrWhiteSpace(line.ItemId) && line.Quantity > 0)
            .Select(line => new AgentCheckoutLineInput
            {
                ItemId = line.ItemId.Trim(),
                Quantity = line.Quantity,
            })
            .ToList();

    internal static List<string> NormalizeCodes(IEnumerable<string> codes)
        => (codes ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string Fingerprint(object value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonSerializerOptions.Web))));
}
