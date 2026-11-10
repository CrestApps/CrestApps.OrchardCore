using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Telemetry;

/// <summary>
/// The meter and activity source of subscriptions, <c>CrestApps.OrchardCore.Subscriptions</c>: the transitions of
/// subscriptions (renewals among them), the charges of installment plans and the provisioning of the sites customers
/// paid for. Subscription, plan and job ids go on spans only; tenant names, amounts and customers go nowhere.
/// </summary>
public static class SubscriptionsTelemetry
{
    /// <summary>
    /// The name of the subscriptions meter and activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Subscriptions";

    /// <summary>
    /// The span tag that carries the id of a subscription. It is never a metric tag.
    /// </summary>
    public const string SubscriptionIdTag = "crestapps.subscriptions.subscription.id";

    /// <summary>
    /// The span tag that carries the id of an installment plan. It is never a metric tag.
    /// </summary>
    public const string PlanIdTag = "crestapps.subscriptions.plan.id";

    /// <summary>
    /// The span tag that carries the id of a tenant provisioning job. It is never a metric tag.
    /// </summary>
    public const string JobIdTag = "crestapps.subscriptions.provisioning.job.id";

    /// <summary>
    /// The tag that records the status of a subscription before a transition.
    /// </summary>
    public const string FromStatusTag = "from";

    /// <summary>
    /// The tag that records the status of a subscription after a transition.
    /// </summary>
    public const string ToStatusTag = "to";

    /// <summary>
    /// The tag that records the attempt of a tenant provisioning job, from 1 to 5.
    /// </summary>
    public const string AttemptTag = "attempt";

    /// <summary>
    /// The <c>operation</c> that records a billing cycle; a changed result is a renewal.
    /// </summary>
    public const string OperationRenew = "renew";

    /// <summary>
    /// The <c>operation</c> that marks a subscription past due after a failed payment.
    /// </summary>
    public const string OperationPastDue = "past_due";

    /// <summary>
    /// The <c>operation</c> that cancels a subscription.
    /// </summary>
    public const string OperationCancel = "cancel";

    /// <summary>
    /// The <c>operation</c> that returns a subscription to active.
    /// </summary>
    public const string OperationResume = "resume";

    /// <summary>
    /// The <c>operation</c> that pauses a subscription.
    /// </summary>
    public const string OperationPause = "pause";

    /// <summary>
    /// The <c>operation</c> that expires a subscription.
    /// </summary>
    public const string OperationExpire = "expire";

    /// <summary>
    /// The <c>operation</c> that applies what the payment provider reports about a subscription.
    /// </summary>
    public const string OperationSync = "sync";

    /// <summary>
    /// The transition changed the subscription.
    /// </summary>
    public const string ResultChanged = "changed";

    /// <summary>
    /// The transition did not apply, such as a renewal of a cycle already recorded.
    /// </summary>
    public const string ResultNoChange = "no_change";

    /// <summary>
    /// Another node was changing the same subscription, so the transition was skipped.
    /// </summary>
    public const string ResultBusy = "busy";

    /// <summary>
    /// The subscription does not exist.
    /// </summary>
    public const string ResultNotFound = "not_found";

    /// <summary>
    /// The installment was charged, or its charge is still settling at the provider.
    /// </summary>
    public const string ChargeCharged = "charged";

    /// <summary>
    /// An earlier charge of the installment was still settling, so it was finished rather than repeated.
    /// </summary>
    public const string ChargePending = "pending";

    /// <summary>
    /// The installment was paid already, so nothing was charged.
    /// </summary>
    public const string ChargeAlreadyPaid = "already_paid";

    /// <summary>
    /// The provider declined the charge.
    /// </summary>
    public const string ChargeDeclined = "declined";

    /// <summary>
    /// The charge could not be made, such as a provider that could not be reached; it is tried again soon.
    /// </summary>
    public const string ChargeTransientFailure = "transient_failure";

    /// <summary>
    /// The installment's transaction could not be found.
    /// </summary>
    public const string ChargeNotFound = "not_found";

    /// <summary>
    /// The <c>trigger</c> of an installment charged on its schedule.
    /// </summary>
    public const string TriggerSchedule = "schedule";

    /// <summary>
    /// The <c>trigger</c> of an installment an administrator charged.
    /// </summary>
    public const string TriggerManual = "manual";

    /// <summary>
    /// The site was created.
    /// </summary>
    public const string ProvisioningSucceeded = "succeeded";

    /// <summary>
    /// The attempt failed and the job will be tried again.
    /// </summary>
    public const string ProvisioningFailed = "failed";

    /// <summary>
    /// The attempt failed and the job ran out of attempts, so a person has to resolve it.
    /// </summary>
    public const string ProvisioningAbandoned = "abandoned";

    /// <summary>
    /// Another node holds the job, so nothing was attempted.
    /// </summary>
    public const string ProvisioningBusy = "busy";

    /// <summary>
    /// The job was finished already, or does not exist, so nothing was attempted.
    /// </summary>
    public const string ProvisioningSkipped = CrestAppsOrchardCoreTelemetry.OutcomeSkipped;

    /// <summary>
    /// The meter of subscriptions.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Subscriptions");

    /// <summary>
    /// The activity source of subscriptions.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Subscriptions");

    /// <summary>
    /// The number of subscription transitions, tagged with the <c>operation</c>, the <c>result</c> and, for a change,
    /// the <see cref="FromStatusTag"/> and the <see cref="ToStatusTag"/>. Renewals are the changed transitions of the
    /// <see cref="OperationRenew"/> operation; a cycle reported twice, by the gateway's webhook and the sweep, is a
    /// <see cref="ResultNoChange"/> and is not counted as a renewal.
    /// </summary>
    public static Counter<long> Transitions { get; } = Meter.CreateCounter<long>(
        "crestapps.subscriptions.transitions",
        "{transition}",
        "The number of subscription transitions, by operation, result and statuses.");

    /// <summary>
    /// The duration of installment charges, tagged with the <c>provider</c>, the <c>trigger</c> and the <c>outcome</c>.
    /// </summary>
    public static Histogram<double> InstallmentChargeDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.subscriptions.installment.charge.duration",
        "The duration of an installment plan charge, through the checkout and its payment provider.");

    /// <summary>
    /// The duration of tenant provisioning attempts, tagged with the <c>outcome</c> and the <see cref="AttemptTag"/>.
    /// </summary>
    public static Histogram<double> ProvisioningDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.subscriptions.provisioning.duration",
        "The duration of an attempt to create a site a customer paid for.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// Records a subscription transition.
    /// </summary>
    /// <param name="operation">The operation, such as <see cref="OperationRenew"/>.</param>
    /// <param name="result">The result, such as <see cref="ResultChanged"/>.</param>
    /// <param name="from">The status before a change, or <see langword="null"/> when nothing changed.</param>
    /// <param name="to">The status after a change, or <see langword="null"/> when nothing changed.</param>
    public static void RecordTransition(string operation, string result, SubscriptionStatus? from = null, SubscriptionStatus? to = null)
    {
        if (!Transitions.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.OperationTag, operation },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        };

        if (from.HasValue && to.HasValue)
        {
            tags.Add(FromStatusTag, GetStatusTag(from.Value));
            tags.Add(ToStatusTag, GetStatusTag(to.Value));
        }

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        Transitions.Add(1, tags);
    }

    /// <summary>
    /// Starts the span of a subscription transition.
    /// </summary>
    /// <param name="operation">The operation, such as <see cref="OperationRenew"/>.</param>
    /// <param name="subscriptionId">The id of the subscription, set on the span only.</param>
    /// <returns>The span, or <see langword="null"/> when nothing listens.</returns>
    public static Activity StartTransition(string operation, string subscriptionId)
    {
        var activity = ActivitySource.StartActivity("crestapps.subscriptions.transition");

        activity?.SetTag(CrestAppsOrchardCoreTelemetry.OperationTag, operation);
        activity?.SetTag(SubscriptionIdTag, subscriptionId);

        return activity;
    }

    /// <summary>
    /// Starts measuring the charge of an installment.
    /// </summary>
    /// <param name="providerKey">The key of the payment provider the plan pays with.</param>
    /// <param name="manual">Whether an administrator asked for the charge.</param>
    /// <param name="planId">The id of the plan, set on the span only.</param>
    /// <returns>The scope, which records the <see cref="InstallmentChargeDuration"/> when disposed.</returns>
    public static TelemetryScope StartInstallmentCharge(string providerKey, bool manual, string planId)
    {
        var scope = TelemetryScope.Start(
            ActivitySource,
            "crestapps.subscriptions.installment.charge",
            InstallmentChargeDuration,
            new TagList
            {
                { CrestAppsOrchardCoreTelemetry.ProviderTag, string.IsNullOrEmpty(providerKey) ? CrestAppsOrchardCoreTelemetry.Unknown : providerKey },
                { CrestAppsOrchardCoreTelemetry.TriggerTag, manual ? TriggerManual : TriggerSchedule },
            });

        scope.SetSpanTag(PlanIdTag, planId);

        return scope;
    }

    /// <summary>
    /// Starts measuring an attempt to provision a tenant.
    /// </summary>
    /// <param name="jobId">The id of the provisioning job, set on the span only.</param>
    /// <returns>The scope, which records the <see cref="ProvisioningDuration"/> when disposed.</returns>
    public static TelemetryScope StartProvisioning(string jobId)
    {
        var scope = TelemetryScope.Start(ActivitySource, "crestapps.subscriptions.provisioning", ProvisioningDuration);

        scope.SetSpanTag(JobIdTag, jobId);

        return scope;
    }

    /// <summary>
    /// Gets the bounded tag value of a subscription status.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The status in snake case, such as <c>past_due</c>.</returns>
    public static string GetStatusTag(SubscriptionStatus status)
    {
        return status switch
        {
            SubscriptionStatus.Active => "active",
            SubscriptionStatus.Trialing => "trialing",
            SubscriptionStatus.PastDue => "past_due",
            SubscriptionStatus.Canceled => "canceled",
            SubscriptionStatus.Expired => "expired",
            SubscriptionStatus.Paused => "paused",
            SubscriptionStatus.Incomplete => "incomplete",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }
}
