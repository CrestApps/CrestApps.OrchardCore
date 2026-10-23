using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// A payment provider for development and automated tests. It confirms every payment at once and moves no money,
/// so a whole checkout, refund, installment plan or subscription can be exercised end to end without gateway keys
/// or a browser that can drive a card form.
/// </summary>
/// <remarks>
/// <para>
/// It behaves like a real gateway in every way the checkout can observe: begin returns a provider reference,
/// verification reports the amount it "charged" as authoritative so the checkout cross-checks it, refunds return a
/// refund reference, and a payment begun with <see cref="CheckoutPaymentDataKeys.SavePaymentMethod"/> keeps a
/// payment method that can be charged again off-session. That is what makes it worth testing against: a flow that
/// works with it uses the same contracts, ledgers and checks as one that works with Stripe.
/// </para>
/// <para>
/// The last two digits of the amount an obligation charges, in the currency's smallest unit, choose an outcome,
/// the way gateway test cards do: <see cref="DeclinedCents"/> is declined, <see cref="PendingCents"/> stays
/// pending until it is <see cref="PendingSettlementDelay"/> old, so the reconciliation sweep settles it, and
/// <see cref="RequiresAuthenticationCents"/> charged to a saved payment method without the payer present is refused
/// because the cardholder would have to approve it.
/// </para>
/// </remarks>
public sealed class TestCheckoutPaymentProvider :
    ICheckoutPaymentProvider,
    ICheckoutPaymentRefundProvider,
    ICheckoutSavedPaymentMethodProvider,
    ICheckoutRecurringPaymentProvider
{
    /// <summary>
    /// The stable key of the test payment provider.
    /// </summary>
    public const string ProviderKey = "test";

    /// <summary>
    /// The last two digits, in the currency's smallest unit, of an amount the test provider declines.
    /// </summary>
    public const int DeclinedCents = 2;

    /// <summary>
    /// The last two digits, in the currency's smallest unit, of an amount the test provider leaves pending.
    /// </summary>
    public const int PendingCents = 5;

    /// <summary>
    /// The last two digits, in the currency's smallest unit, of an amount the test provider refuses to charge to a
    /// saved payment method without the payer present, because the card's bank "asks the cardholder to approve it".
    /// A payment the payer makes themselves with such an amount is approved as usual.
    /// </summary>
    public const int RequiresAuthenticationCents = 3;

    /// <summary>
    /// How long a payment with <see cref="PendingCents"/> stays pending after it was begun. It matches the age at
    /// which the reconciliation sweep picks a pending attempt up, so the browser gives up waiting and the sweep
    /// is what completes the checkout.
    /// </summary>
    public static readonly TimeSpan PendingSettlementDelay = TimeSpan.FromMinutes(2);

    private const string PaymentReferencePrefix = "test_pay_";
    private const string SavedPaymentReferencePrefix = "test_save_";
    private const string SubscriptionReferencePrefix = "test_sub_";
    private const string RefundReferencePrefix = "test_re_";
    private const string CustomerReferencePrefix = "test_cus_";
    private const string PaymentMethodReferencePrefix = "test_pm_";

    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestCheckoutPaymentProvider"/> class.
    /// </summary>
    /// <param name="clock">The clock used to age pending payments and to date the saved payment method.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TestCheckoutPaymentProvider(
        IClock clock,
        IStringLocalizer<TestCheckoutPaymentProvider> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Key => ProviderKey;

    /// <inheritdoc/>
    public string DisplayName => S["Test payment"];

    /// <inheritdoc/>
    public PaymentProviderCapabilities Capabilities { get; } = new()
    {
        SupportsOneTimePayments = true,
        SupportsRecurringPayments = true,
        SupportsHostedCheckout = false,

        // Its panel is shown on the page and needs no input. Claiming in-page collection is what lets the screens
        // that only offer in-page methods, such as an installment plan's down payment, offer it too.
        SupportsEmbeddedElements = true,
        SupportsCombinedOneTimeAndRecurring = true,
        CollectsTaxDynamically = false,
        SupportsRefunds = true,
        SupportsSavedPaymentMethods = true,

        // It stands in for a gateway that collects money now, so it can pay off an existing balance.
        DefersPayment = false,
    };

    /// <inheritdoc/>
    public Task<PaymentBeginResult> BeginAsync(BeginPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Attempt);

        var attempt = context.Attempt;

        var offSession = CheckoutPaymentDataKeys.IsSet(context.ProviderData, CheckoutPaymentDataKeys.OffSession);

        if (offSession && !IsSavedPaymentMethod(context.ProviderData))
        {
            return Task.FromResult(PaymentBeginResult.Failure(S["There is no saved test payment method to charge."]));
        }

        if (offSession && GetMagicCents(attempt) == RequiresAuthenticationCents)
        {
            return Task.FromResult(PaymentBeginResult.AuthenticationRequired(
                S["The test card's bank asked the cardholder to approve this payment, because the amount ends in .03."],
                PaymentReferencePrefix + IdGenerator.GenerateId()));
        }

        var prefix = CheckoutPaymentDataKeys.IsSet(context.ProviderData, CheckoutPaymentDataKeys.SavePaymentMethod)
            ? SavedPaymentReferencePrefix
            : PaymentReferencePrefix;

        return Task.FromResult(Begin(attempt, prefix));
    }

    /// <inheritdoc/>
    public Task<PaymentVerificationResult> VerifyAsync(VerifyPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Attempt);

        var attempt = context.Attempt;

        // An attempt that was created but never begun has nothing to confirm yet.
        if (string.IsNullOrEmpty(attempt.ProviderReference))
        {
            return Task.FromResult(new PaymentVerificationResult
            {
                Status = PaymentStatus.Unknown,
            });
        }

        if (GetMagicCents(attempt) == PendingCents && !IsOldEnoughToSettle(attempt))
        {
            return Task.FromResult(new PaymentVerificationResult
            {
                Status = PaymentStatus.Unknown,
                Currency = attempt.Currency,
                GatewayMode = GatewayMode.Testing,
            });
        }

        return Task.FromResult(new PaymentVerificationResult
        {
            Status = PaymentStatus.Succeeded,

            // Reported as authoritative so the checkout cross-checks the amount and currency exactly as it does
            // for a real gateway. Reporting it as a deferred provider does would skip that check, and a flow that
            // passes here could then fail against the real thing.
            ReportsAuthoritativeAmount = true,
            TransactionId = attempt.ProviderReference,
            Amount = attempt.ExpectedAmount,
            TaxAmount = attempt.ExpectedTaxAmount,
            TaxSnapshot = attempt.TaxSnapshot,
            Currency = attempt.Currency,
            GatewayMode = GatewayMode.Testing,
        });
    }

    /// <inheritdoc/>
    public Task<PaymentCancelResult> CancelAsync(CancelPaymentContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(PaymentCancelResult.Success());

    /// <inheritdoc/>
    public Task<PaymentRefundResult> RefundAsync(RefundPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(context.OriginalTransactionId);

        // The reference is derived from the idempotency key, so a retried refund returns the same refund rather
        // than a second one, as a gateway honoring the key would.
        var reference = string.IsNullOrEmpty(context.IdempotencyKey)
            ? RefundReferencePrefix + IdGenerator.GenerateId()
            : RefundReferencePrefix + context.IdempotencyKey;

        return Task.FromResult(PaymentRefundResult.Success(reference, context.Amount, context.Currency, GatewayMode.Testing));
    }

    /// <inheritdoc/>
    public Task<SavedPaymentMethod> GetSavedPaymentMethodAsync(PaymentAttempt attempt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        // Only a payment begun with the request to keep its payment method left one behind.
        if (string.IsNullOrEmpty(attempt.ProviderReference) ||
            !attempt.ProviderReference.StartsWith(SavedPaymentReferencePrefix, StringComparison.Ordinal))
        {
            return Task.FromResult<SavedPaymentMethod>(null);
        }

        return Task.FromResult(new SavedPaymentMethod
        {
            ProviderKey = Key,
            CustomerReference = CustomerReferencePrefix + attempt.SessionId,
            PaymentMethodReference = PaymentMethodReferencePrefix + attempt.ItemId,
            Brand = "test",
            Last4 = "4242",
            ExpirationMonth = 12,
            ExpirationYear = _clock.UtcNow.Year + 5,
        });
    }

    /// <inheritdoc/>
    public Task<PaymentBeginResult> BeginRecurringAsync(BeginRecurringPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Attempt);

        return Task.FromResult(Begin(context.Attempt, SubscriptionReferencePrefix));
    }

    /// <inheritdoc/>
    public Task<RecurringCancelResult> CancelRecurringAsync(CancelRecurringPaymentContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(RecurringCancelResult.Success());

    /// <inheritdoc/>
    public Task<RecurringPauseResult> PauseRecurringAsync(PauseRecurringPaymentContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(RecurringPauseResult.Success());

    /// <inheritdoc/>
    public Task<RecurringUpdateResult> UpdateRecurringAsync(UpdateRecurringPaymentContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(RecurringUpdateResult.Success());

    // Begins one obligation. A resumed attempt keeps the reference it was given the first time, so retrying a begin
    // never turns one payment into two.
    private PaymentBeginResult Begin(PaymentAttempt attempt, string prefix)
    {
        var reference = string.IsNullOrEmpty(attempt.ProviderReference)
            ? prefix + IdGenerator.GenerateId()
            : attempt.ProviderReference;

        if (GetMagicCents(attempt) == DeclinedCents)
        {
            return PaymentBeginResult.Decline(S["The test payment was declined, because the amount ends in .02."], reference);
        }

        // Nothing for the customer to do: the payment is confirmed when the checkout verifies it.
        return PaymentBeginResult.Success(reference);
    }

    private bool IsOldEnoughToSettle(PaymentAttempt attempt)
        => attempt.CreatedUtc == default || _clock.UtcNow - attempt.CreatedUtc >= PendingSettlementDelay;

    // The last two digits of what the obligation charges, tax included, in the currency's smallest unit. That is the
    // amount a tester reads on the page, so it is the one that chooses the outcome.
    private static long GetMagicCents(PaymentAttempt attempt)
        => Math.Abs(Money.ToMinorUnits(attempt.ExpectedAmount + attempt.ExpectedTaxAmount, attempt.Currency)) % 100;

    private static bool IsSavedPaymentMethod(IReadOnlyDictionary<string, string> providerData)
        => providerData.TryGetValue(CheckoutPaymentDataKeys.SavedCustomerReference, out var customer) &&
            providerData.TryGetValue(CheckoutPaymentDataKeys.SavedPaymentMethodReference, out var paymentMethod) &&
            customer is not null &&
            paymentMethod is not null &&
            customer.StartsWith(CustomerReferencePrefix, StringComparison.Ordinal) &&
            paymentMethod.StartsWith(PaymentMethodReferencePrefix, StringComparison.Ordinal);
}
