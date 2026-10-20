using CrestApps.OrchardCore.Commerce.Api;

namespace CrestApps.OrchardCore.Checkout.Core.Api;

/// <summary>
/// A checkout: its steps and what's saved on them, and the invoice as it stands, with what's left to pay.
/// </summary>
/// <param name="Id">The checkout's id.</param>
/// <param name="Status"><c>pending</c>, <c>awaitingProvider</c>, <c>paymentPending</c>, <c>completed</c>, <c>failed</c>, <c>canceled</c> or <c>expired</c>.</param>
/// <param name="OrderId">The order it pays for, when it pays for one.</param>
/// <param name="OrderNumber">The number the buyer knows the order by.</param>
/// <param name="Currency">The currency it's charged in.</param>
/// <param name="Steps">The steps, in order, and whether each is done.</param>
/// <param name="NextStep">The key of the first step still to do, such as <c>Contact</c> or <c>Shipping</c>; <c>Payment</c> when only paying is left; none once it's finished.</param>
/// <param name="Contact">Who the buyer is, from the Contact step.</param>
/// <param name="Shipping">Where and how the order goes, from the Shipping step.</param>
/// <param name="Lines">What's being paid for.</param>
/// <param name="Discounts">The discounts taken off, from codes and automatic offers.</param>
/// <param name="Codes">The codes the buyer entered.</param>
/// <param name="Tenders">The balances paying toward it: gift cards, store credit, points.</param>
/// <param name="Totals">What it comes to.</param>
/// <param name="CompletedUtc">When it was completed.</param>
public sealed record ApiCheckout(
    string Id,
    string Status,
    string OrderId,
    string OrderNumber,
    string Currency,
    IReadOnlyList<ApiCheckoutStep> Steps,
    string NextStep,
    ApiCheckoutContact Contact,
    ApiCheckoutShipping Shipping,
    IReadOnlyList<ApiCheckoutLine> Lines,
    IReadOnlyList<ApiCheckoutDiscount> Discounts,
    IReadOnlyList<string> Codes,
    IReadOnlyList<ApiCheckoutTender> Tenders,
    ApiCheckoutTotals Totals,
    DateTime? CompletedUtc);

/// <summary>
/// A step of the checkout. The Payment step is done once the checkout is paid and completed.
/// </summary>
public sealed record ApiCheckoutStep(string Key, string Title, bool IsDone);

/// <summary>
/// The buyer's name, email, phone and billing address.
/// </summary>
public sealed record ApiCheckoutContact(string Name, string Email, string Phone, ApiAddress BillingAddress);

/// <summary>
/// Where the order goes and how: the rate chosen and the address, which for a pickup is the location's.
/// </summary>
public sealed record ApiCheckoutShipping(string RateId, string Name, string Description, ApiMoney Amount, ApiAddress Address);

/// <summary>
/// A line of the invoice.
/// </summary>
/// <param name="Id">The line's id: the cart line, or a fixed id such as the shipping line's.</param>
/// <param name="ProductId">The product, when the line is one.</param>
/// <param name="Description">What the line is for.</param>
/// <param name="Interval">How often a recurring line charges: <c>day</c>, <c>week</c>, <c>month</c> or <c>year</c>.</param>
/// <param name="IntervalCount">How many intervals pass between a recurring line's charges.</param>
public sealed record ApiCheckoutLine(
    string Id,
    string ProductId,
    string Sku,
    string Description,
    int Quantity,
    ApiMoney UnitPrice,
    ApiMoney Discount,
    ApiMoney Total,
    string Interval,
    int? IntervalCount);

/// <summary>
/// A discount taken off the invoice.
/// </summary>
/// <param name="Target">What it comes off: <c>oneTime</c> (goods and fees paid now), <c>firstCycle</c> (a subscription's first charge) or <c>shipping</c>.</param>
public sealed record ApiCheckoutDiscount(string Code, string Description, ApiMoney Amount, string Target, bool IsAutomatic);

/// <summary>
/// A balance paying toward the invoice.
/// </summary>
/// <param name="Provider">What kind: <c>gift-card</c>, <c>store-credit</c> or <c>loyalty-points</c>.</param>
/// <param name="Reference">Which one: the gift card, the store credit account, the points account.</param>
public sealed record ApiCheckoutTender(string Provider, string Reference, string Description, ApiMoney Amount);

/// <summary>
/// What the checkout comes to.
/// </summary>
/// <param name="Subtotal">The goods and fees before discounts.</param>
/// <param name="Shipping">The shipping, before a shipping discount.</param>
/// <param name="Discount">The discounts taken off.</param>
/// <param name="Tax">The tax.</param>
/// <param name="Total">What the order costs.</param>
/// <param name="Tendered">What gift cards, store credit and points pay.</param>
/// <param name="AmountDue">What's left to pay now, by card or another payment method.</param>
public sealed record ApiCheckoutTotals(ApiMoney Subtotal, ApiMoney Shipping, ApiMoney Discount, ApiMoney Tax, ApiMoney Total, ApiMoney Tendered, ApiMoney AmountDue);

/// <summary>
/// A way to pay what's due.
/// </summary>
/// <param name="Key">The payment method's key, to begin a payment with it.</param>
/// <param name="Title">Its name.</param>
/// <param name="Description">What paying this way means for the buyer.</param>
/// <param name="IsDefault">Whether it's the one chosen when none is.</param>
/// <param name="Kind"><c>gateway</c>, paid now through the provider; or <c>offline</c>, confirmed now and paid later.</param>
/// <param name="Client">What a client needs to collect the payment itself, such as Stripe's publishable key.</param>
public sealed record ApiPaymentMethod(string Key, string Title, string Description, bool IsDefault, string Kind, IReadOnlyDictionary<string, string> Client);

/// <summary>
/// A payment begun: for each charge, what the client does next.
/// </summary>
public sealed record ApiPaymentBegun(bool RequiresAction, IReadOnlyList<ApiPaymentStep> Steps);

/// <summary>
/// What the client does for one charge: confirm it with the provider's client SDK using the client secret, or send
/// the buyer to the provider's page.
/// </summary>
public sealed record ApiPaymentStep(string ObligationId, string AttemptId, string ClientSecret, string RedirectUrl, bool RequiresAction);

/// <summary>
/// Where completing the checkout got to.
/// </summary>
/// <param name="Status"><c>completed</c>, <c>alreadyCompleted</c>, <c>pending</c> (the provider hasn't answered: ask again shortly), <c>blocked</c> (a step isn't done), <c>failed</c> or <c>canceled</c>.</param>
/// <param name="Completed">Whether it's paid for and done.</param>
/// <param name="BlockingStep">The step to do first, when it's blocked.</param>
/// <param name="Error">Why it failed or is blocked.</param>
/// <param name="OrderId">The order it paid for.</param>
/// <param name="OrderNumber">The number the buyer knows the order by.</param>
public sealed record ApiCheckoutCompletion(string Status, bool Completed, string BlockingStep, string Error, string OrderId, string OrderNumber);
