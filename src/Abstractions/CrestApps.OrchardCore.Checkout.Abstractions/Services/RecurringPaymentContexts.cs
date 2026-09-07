using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The input for <see cref="ICheckoutRecurringPaymentProvider.BeginRecurringAsync"/>. It carries the durable
/// attempt the agreement settles, the interval being established, and the lines that make it up.
/// </summary>
public sealed class BeginRecurringPaymentContext
{
    /// <summary>
    /// Gets or sets the checkout session the agreement belongs to.
    /// </summary>
    public CheckoutSession Session { get; set; }

    /// <summary>
    /// Gets or sets the durable attempt for this obligation. It is already persisted before the provider is
    /// called, so a crash cannot leave the remote agreement untracked.
    /// </summary>
    public PaymentAttempt Attempt { get; set; }

    /// <summary>
    /// Gets or sets the invoice describing the whole checkout.
    /// </summary>
    public CheckoutInvoice Invoice { get; set; }

    /// <summary>
    /// Gets or sets the billing interval this agreement bills on.
    /// </summary>
    public BillingDurationKey Interval { get; set; }

    /// <summary>
    /// Gets or sets the invoice lines that share this interval and are therefore billed together as one
    /// agreement.
    /// </summary>
    public IList<CheckoutLineItem> LineItems { get; set; } = [];

    /// <summary>
    /// Gets or sets the values the provider's own client script collected before the payment began, for
    /// example a tokenized payment method. A gateway usually cannot create a recurring agreement without a
    /// reusable payment method, and that method can only be produced in the customer's browser.
    /// </summary>
    public IReadOnlyDictionary<string, string> ProviderData { get; set; }

    /// <summary>
    /// Gets or sets the free trial, in days, before the first cycle is billed.
    /// </summary>
    /// <remarks>
    /// The gateway is told about the trial rather than the checkout simply not charging, so the gateway is
    /// the one holding the schedule. Anything else means the site has to remember to start billing later,
    /// which is exactly the kind of promise a restart breaks.
    /// </remarks>
    public int? TrialDays { get; set; }

    /// <summary>
    /// Gets or sets the absolute URL a hosted provider returns the customer to after a successful payment.
    /// </summary>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets the absolute URL a hosted provider returns the customer to when they cancel.
    /// </summary>
    public string CancelUrl { get; set; }
}

/// <summary>
/// The input for <see cref="ICheckoutRecurringPaymentProvider.CancelRecurringAsync"/>.
/// </summary>
public sealed class CancelRecurringPaymentContext
{
    /// <summary>
    /// Gets or sets the provider's reference for the agreement to cancel.
    /// </summary>
    public string ProviderSubscriptionId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the agreement should stop at the end of the period the
    /// customer has already paid for, rather than immediately. Cancelling immediately when the customer has
    /// paid through a later date takes away access they are entitled to.
    /// </summary>
    public bool AtPeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets the reason recorded for audit.
    /// </summary>
    public string Reason { get; set; }
}

/// <summary>
/// The input for <see cref="ICheckoutRecurringPaymentProvider.UpdateRecurringAsync"/>.
/// </summary>
public sealed class UpdateRecurringPaymentContext
{
    /// <summary>
    /// Gets or sets the provider's reference for the agreement to change.
    /// </summary>
    public string ProviderSubscriptionId { get; set; }

    /// <summary>
    /// Gets or sets the new amount billed each cycle, in major currency units.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency of <see cref="Amount"/>.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the new quantity.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the billing interval the changed agreement bills on.
    /// </summary>
    public BillingDurationKey Interval { get; set; }

    /// <summary>
    /// Gets or sets a short description used as the remote price's label.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets how the change is prorated. Getting this wrong either over-charges a customer who
    /// upgraded mid-cycle or gives away time they did not pay for, so it is explicit rather than assumed.
    /// </summary>
    public RecurringProrationBehavior Proration { get; set; } = RecurringProrationBehavior.CreateProrations;

    /// <summary>
    /// Gets or sets the deterministic idempotency key so a retried change is applied once.
    /// </summary>
    public string IdempotencyKey { get; set; }
}

/// <summary>
/// How a mid-cycle change to a recurring agreement is charged.
/// </summary>
public enum RecurringProrationBehavior
{
    /// <summary>
    /// Credit the unused portion of the current cycle and charge the new amount pro rata.
    /// </summary>
    CreateProrations,

    /// <summary>
    /// Apply the change without any proration; the new amount takes effect on the next cycle.
    /// </summary>
    None,

    /// <summary>
    /// Prorate and bill the difference immediately rather than on the next cycle.
    /// </summary>
    AlwaysInvoice,
}

/// <summary>
/// The result of cancelling a recurring agreement.
/// </summary>
public sealed class RecurringCancelResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the provider confirmed the cancellation.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the agreement ends. When the cancellation takes effect at the end of the
    /// paid period this is that date, which the caller uses to keep access until then.
    /// </summary>
    public DateTime? EndsUtc { get; set; }

    /// <summary>
    /// Gets or sets the error message when the provider could not cancel the agreement.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Creates a confirmed cancellation result.
    /// </summary>
    /// <param name="endsUtc">The date the agreement ends, when the provider reports one.</param>
    public static RecurringCancelResult Success(DateTime? endsUtc = null)
        => new() { Succeeded = true, EndsUtc = endsUtc };

    /// <summary>
    /// Creates a failed cancellation result.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    public static RecurringCancelResult Failure(string errorMessage)
        => new() { Succeeded = false, ErrorMessage = errorMessage };
}

/// <summary>
/// The result of changing a recurring agreement.
/// </summary>
public sealed class RecurringUpdateResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the provider confirmed the change.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the UTC end of the period the change takes effect from, when the provider reports one.
    /// </summary>
    public DateTime? CurrentPeriodEndUtc { get; set; }

    /// <summary>
    /// Gets or sets the error message when the provider could not apply the change.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Creates a confirmed update result.
    /// </summary>
    /// <param name="currentPeriodEndUtc">The end of the period the change takes effect from.</param>
    public static RecurringUpdateResult Success(DateTime? currentPeriodEndUtc = null)
        => new() { Succeeded = true, CurrentPeriodEndUtc = currentPeriodEndUtc };

    /// <summary>
    /// Creates a failed update result.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    public static RecurringUpdateResult Failure(string errorMessage)
        => new() { Succeeded = false, ErrorMessage = errorMessage };
}
