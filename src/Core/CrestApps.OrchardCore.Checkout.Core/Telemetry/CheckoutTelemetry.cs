using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Checkout.Core.Telemetry;

/// <summary>
/// The meter and activity source of the checkout, <c>CrestApps.OrchardCore.Checkout</c>, and its instruments: how
/// checkouts end, how payments begin, what reconciliation finds and what had to be given back. Ids of sessions,
/// attempts, orders and payments go on spans only; amounts and card data go nowhere.
/// </summary>
public static class CheckoutTelemetry
{
    /// <summary>
    /// The name of the checkout's meter and activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Checkout";

    /// <summary>
    /// The span tag that carries the id of the checkout session. It is never a metric tag.
    /// </summary>
    public const string SessionIdTag = "crestapps.checkout.session.id";

    /// <summary>
    /// The span tag that carries the id of a payment attempt. It is never a metric tag.
    /// </summary>
    public const string AttemptIdTag = "crestapps.checkout.attempt.id";

    /// <summary>
    /// The <see cref="CrestAppsOrchardCoreTelemetry.OperationTag"/> of starting a checkout.
    /// </summary>
    public const string OperationStart = "start";

    /// <summary>
    /// The <see cref="CrestAppsOrchardCoreTelemetry.OperationTag"/> of beginning a checkout's payment.
    /// </summary>
    public const string OperationBeginPayment = "begin_payment";

    /// <summary>
    /// The <see cref="CrestAppsOrchardCoreTelemetry.OperationTag"/> of completing a checkout.
    /// </summary>
    public const string OperationComplete = "complete";

    /// <summary>
    /// The <see cref="CrestAppsOrchardCoreTelemetry.OperationTag"/> of canceling a checkout.
    /// </summary>
    public const string OperationCancel = "cancel";

    /// <summary>
    /// The <see cref="CrestAppsOrchardCoreTelemetry.OperationTag"/> of expiring a checkout nobody finished.
    /// </summary>
    public const string OperationExpire = "expire";

    /// <summary>
    /// A checkout was completed: every obligation was confirmed and the purchase fulfilled.
    /// </summary>
    public const string SessionCompleted = "completed";

    /// <summary>
    /// A checkout failed, because a payment was declined.
    /// </summary>
    public const string SessionFailed = "failed";

    /// <summary>
    /// A checkout was canceled.
    /// </summary>
    public const string SessionCancelled = "cancelled";

    /// <summary>
    /// A checkout expired before anyone finished it.
    /// </summary>
    public const string SessionExpired = "expired";

    /// <summary>
    /// The provider accepted the payment without anything more from the buyer.
    /// </summary>
    public const string PaymentBeginOk = CrestAppsOrchardCoreTelemetry.OutcomeOk;

    /// <summary>
    /// The provider accepted the payment, and the buyer has to confirm it, such as on a redirect.
    /// </summary>
    public const string PaymentBeginRequiresAction = "requires_action";

    /// <summary>
    /// The provider declined the payment.
    /// </summary>
    public const string PaymentBeginDeclined = "declined";

    /// <summary>
    /// The provider declined the payment because the card needs the buyer to authenticate.
    /// </summary>
    public const string PaymentBeginRequiresAuthentication = "requires_auth";

    /// <summary>
    /// The checkout refused to begin the payment before any provider was asked; the <c>reason</c> tag says why.
    /// </summary>
    public const string PaymentBeginRefused = "refused";

    /// <summary>
    /// A settled payment was given back, because the checkout it paid for could not complete.
    /// </summary>
    public const string CompensationRefunded = "refunded";

    /// <summary>
    /// A settled payment could not be given back automatically and has to be refunded by hand.
    /// </summary>
    public const string CompensationFailed = "failed";

    /// <summary>
    /// The provider confirmed the payment attempt.
    /// </summary>
    public const string ReconcileSettled = "settled";

    /// <summary>
    /// The provider reported the payment attempt failed.
    /// </summary>
    public const string ReconcileFailed = "failed";

    /// <summary>
    /// The provider has no final answer for the payment attempt yet.
    /// </summary>
    public const string ReconcileStillPending = "still_pending";

    /// <summary>
    /// The provider reported success, but the confirmation did not match what the attempt expected.
    /// </summary>
    public const string ReconcileRejected = "rejected";

    /// <summary>
    /// The provider of the payment attempt is not registered, so it could not be asked.
    /// </summary>
    public const string ReconcileNoProvider = "no_provider";

    /// <summary>
    /// The meter of the checkout.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Checkout");

    /// <summary>
    /// The activity source of the checkout.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Checkout");

    /// <summary>
    /// The duration of each call of the checkout engine, tagged with the <c>operation</c>, the <c>outcome</c> and the
    /// <c>result</c> the engine returned.
    /// </summary>
    public static Histogram<double> Duration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.checkout.duration",
        "The duration of a call of the checkout engine.");

    /// <summary>
    /// The number of checkouts that reached an end, tagged with the <c>outcome</c>: <see cref="SessionCompleted"/>,
    /// <see cref="SessionFailed"/>, <see cref="SessionCancelled"/> or <see cref="SessionExpired"/>.
    /// </summary>
    public static Counter<long> Sessions { get; } = Meter.CreateCounter<long>(
        "crestapps.checkout.sessions",
        "{session}",
        "The number of checkouts that were completed, failed, canceled or expired.");

    /// <summary>
    /// The time from a checkout's start to its end, tagged with the <c>outcome</c> of <see cref="Sessions"/>.
    /// </summary>
    public static Histogram<double> SessionDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.checkout.session.duration",
        "The time from a checkout's start to its completion, failure, cancellation or expiry.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// The number of times a checkout's payment was begun, tagged with the <c>provider</c>, the <c>outcome</c> and,
    /// for a refusal, the <c>reason</c>.
    /// </summary>
    public static Counter<long> PaymentBegins { get; } = Meter.CreateCounter<long>(
        "crestapps.checkout.payment.begin",
        "{payment}",
        "The number of times a checkout's payment was begun, by provider and how it went.");

    /// <summary>
    /// The number of settled payments given back because their checkout could not complete, tagged with the
    /// <c>provider</c> and the <c>result</c>: <see cref="CompensationRefunded"/> or <see cref="CompensationFailed"/>.
    /// </summary>
    public static Counter<long> Compensations { get; } = Meter.CreateCounter<long>(
        "crestapps.checkout.compensations",
        "{payment}",
        "The number of settled payments given back because their checkout could not complete.");

    /// <summary>
    /// The number of checkouts whose payment was confirmed but whose fulfillment failed and was left to the
    /// reconciliation sweep. Any steady rate means buyers pay and wait for what they bought.
    /// </summary>
    public static Counter<long> FulfillmentsDeferred { get; } = Meter.CreateCounter<long>(
        "crestapps.checkout.fulfillment.deferred",
        "{session}",
        "The number of paid checkouts whose fulfillment failed and was left to the reconciliation sweep.");

    /// <summary>
    /// The number of times a payment attempt was checked against its provider, tagged with the <c>provider</c> and
    /// the <c>result</c>. An attempt is counted every time it is checked, so one slow payment counts many times.
    /// </summary>
    public static Counter<long> ReconcileAttempts { get; } = Meter.CreateCounter<long>(
        "crestapps.checkout.reconcile.attempts",
        "{attempt}",
        "The number of times a payment attempt was checked against its provider, by what the provider said.");

    /// <summary>
    /// The age of the oldest checkout the reconciliation sweep found waiting on a provider or on its fulfillment,
    /// recorded by each sweep; zero when there is none.
    /// </summary>
    public static Gauge<double> OldestPendingAge { get; } = Meter.CreateGauge<double>(
        "crestapps.checkout.reconcile.oldest_pending_age",
        "s",
        "The age of the oldest checkout the reconciliation sweep found waiting on a provider or on its fulfillment.");

    /// <summary>
    /// The number of checkouts the reconciliation sweep found waiting on a provider or on their fulfillment.
    /// </summary>
    public static Gauge<long> PendingSessions { get; } = Meter.CreateGauge<long>(
        "crestapps.checkout.reconcile.pending",
        "{session}",
        "The number of checkouts the reconciliation sweep found waiting on a provider or on their fulfillment.");

    /// <summary>
    /// Starts measuring a call of the checkout engine.
    /// </summary>
    /// <param name="operation">The operation, such as <see cref="OperationComplete"/>.</param>
    /// <param name="sessionId">The id of the checkout session, set on the span only, or <see langword="null"/>.</param>
    /// <returns>The scope, which records the <see cref="Duration"/> when disposed.</returns>
    public static TelemetryScope StartOperation(string operation, string sessionId)
    {
        var scope = TelemetryScope.Start(
            ActivitySource,
            "crestapps.checkout." + operation,
            Duration,
            new TagList
            {
                { CrestAppsOrchardCoreTelemetry.OperationTag, operation },
            });

        if (!string.IsNullOrEmpty(sessionId))
        {
            scope.SetSpanTag(SessionIdTag, sessionId);
        }

        return scope;
    }

    /// <summary>
    /// Records that a call of the checkout engine returned, with the status it returned as the <c>result</c>.
    /// </summary>
    /// <param name="scope">The scope of the call.</param>
    /// <param name="status">The status the engine returned.</param>
    public static void SetResult(TelemetryScope scope, CheckoutCompletionStatus status)
    {
        ArgumentNullException.ThrowIfNull(scope);

        scope.AddMetricTag(CrestAppsOrchardCoreTelemetry.ResultTag, GetResult(status));
        scope.Succeed();
    }

    /// <summary>
    /// Gets the bounded <c>result</c> tag value of a checkout completion status.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The status in snake case, such as <c>already_completed</c>.</returns>
    public static string GetResult(CheckoutCompletionStatus status)
    {
        return status switch
        {
            CheckoutCompletionStatus.Completed => "completed",
            CheckoutCompletionStatus.AlreadyCompleted => "already_completed",
            CheckoutCompletionStatus.Pending => "pending",
            CheckoutCompletionStatus.Blocked => "blocked",
            CheckoutCompletionStatus.Failed => "failed",
            CheckoutCompletionStatus.Canceled => "cancelled",
            CheckoutCompletionStatus.NotFound => "not_found",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }

    /// <summary>
    /// Gets the bounded <c>provider</c> tag value of a payment provider: its key once it is resolved, never a key a
    /// request named, since a request can name any provider.
    /// </summary>
    /// <param name="provider">The resolved provider, or <see langword="null"/>.</param>
    /// <returns>The provider's key, or <c>unknown</c>.</returns>
    public static string GetProviderTag(ICheckoutPaymentProvider provider)
    {
        return string.IsNullOrEmpty(provider?.Key)
            ? CrestAppsOrchardCoreTelemetry.Unknown
            : provider.Key;
    }

    /// <summary>
    /// Records that a checkout reached an end.
    /// </summary>
    /// <param name="outcome">How it ended: <see cref="SessionCompleted"/>, <see cref="SessionFailed"/>,
    /// <see cref="SessionCancelled"/> or <see cref="SessionExpired"/>.</param>
    /// <param name="elapsed">The time since the checkout started.</param>
    public static void RecordSessionEnded(string outcome, TimeSpan elapsed)
    {
        if (!Sessions.Enabled && !SessionDuration.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);

        Sessions.Add(1, tags);

        if (elapsed >= TimeSpan.Zero)
        {
            SessionDuration.Record(elapsed.TotalSeconds, tags);
        }
    }

    /// <summary>
    /// Records that a checkout's payment was begun.
    /// </summary>
    /// <param name="provider">The provider tag, from <see cref="GetProviderTag(ICheckoutPaymentProvider)"/>.</param>
    /// <param name="outcome">How it went, such as <see cref="PaymentBeginDeclined"/>.</param>
    /// <param name="reason">Why it was refused, from a bounded set, or <see langword="null"/>.</param>
    public static void RecordPaymentBegin(string provider, string outcome, string reason = null)
    {
        if (!PaymentBegins.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ProviderTag, provider ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome },
        };

        if (!string.IsNullOrEmpty(reason))
        {
            tags.Add(CrestAppsOrchardCoreTelemetry.ReasonTag, reason);
        }

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        PaymentBegins.Add(1, tags);
    }

    /// <summary>
    /// Records a settled payment that was given back, or could not be.
    /// </summary>
    /// <param name="provider">The key of the provider that took the payment.</param>
    /// <param name="result"><see cref="CompensationRefunded"/> or <see cref="CompensationFailed"/>.</param>
    public static void RecordCompensation(string provider, string result)
    {
        Add(Compensations, provider, result);
    }

    /// <summary>
    /// Records what a provider said when a payment attempt was checked against it.
    /// </summary>
    /// <param name="provider">The key of the attempt's provider.</param>
    /// <param name="result">What the provider said, such as <see cref="ReconcileSettled"/>.</param>
    public static void RecordReconcileAttempt(string provider, string result)
    {
        Add(ReconcileAttempts, provider, result);
    }

    /// <summary>
    /// Records a paid checkout whose fulfillment failed and was left to the reconciliation sweep.
    /// </summary>
    public static void RecordFulfillmentDeferred()
    {
        if (!FulfillmentsDeferred.Enabled)
        {
            return;
        }

        var tags = new TagList();

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        FulfillmentsDeferred.Add(1, tags);
    }

    /// <summary>
    /// Records what the reconciliation sweep found waiting.
    /// </summary>
    /// <param name="pendingSessions">The number of checkouts waiting on a provider or on their fulfillment.</param>
    /// <param name="oldestPendingAge">The age of the oldest of them, or <see cref="TimeSpan.Zero"/> when there is none.</param>
    public static void RecordPending(long pendingSessions, TimeSpan oldestPendingAge)
    {
        var tags = new TagList();

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);

        if (PendingSessions.Enabled)
        {
            PendingSessions.Record(pendingSessions, tags);
        }

        if (OldestPendingAge.Enabled)
        {
            OldestPendingAge.Record(Math.Max(0d, oldestPendingAge.TotalSeconds), tags);
        }
    }

    private static void Add(Counter<long> counter, string provider, string result)
    {
        if (!counter.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ProviderTag, string.IsNullOrEmpty(provider) ? CrestAppsOrchardCoreTelemetry.Unknown : provider },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        counter.Add(1, tags);
    }
}
