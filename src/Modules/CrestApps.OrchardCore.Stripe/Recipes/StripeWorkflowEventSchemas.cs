using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using CrestApps.OrchardCore.Stripe.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Stripe.Recipes;

/// <summary>
/// Describes the Stripe workflow events to CrestApps Recipes. They have no settings and one <c>Done</c> outcome.
/// </summary>
internal abstract class StripeEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Stripe";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}

/// <summary>
/// Describes the <c>StripePaymentReceivedEvent</c> workflow event.
/// </summary>
internal sealed class StripePaymentReceivedEventSchema : StripeEventSchemaBase
{
    public override string Name => StripeWorkflowEventNames.PaymentReceived;

    protected override string DisplayText => "Stripe Payment Received";

    protected override string Description => "Starts when Stripe reports a received payment.";
}

/// <summary>
/// Describes the <c>StripeSubscriptionCreatedEvent</c> workflow event.
/// </summary>
internal sealed class StripeSubscriptionCreatedEventSchema : StripeEventSchemaBase
{
    public override string Name => StripeWorkflowEventNames.SubscriptionCreated;

    protected override string DisplayText => "Stripe Subscription Created";

    protected override string Description => "Starts when Stripe reports a new subscription.";
}

/// <summary>
/// Describes the <c>StripePaymentIntentSucceededEvent</c> workflow event.
/// </summary>
internal sealed class StripePaymentIntentSucceededEventSchema : StripeEventSchemaBase
{
    public override string Name => StripeWorkflowEventNames.PaymentIntentSucceeded;

    protected override string DisplayText => "Stripe Payment Intent Succeeded";

    protected override string Description => "Starts when Stripe reports a payment intent that succeeded.";
}

/// <summary>
/// Describes the <c>StripePaymentFailedEvent</c> workflow event.
/// </summary>
internal sealed class StripePaymentFailedEventSchema : StripeEventSchemaBase
{
    public override string Name => StripeWorkflowEventNames.PaymentFailed;

    protected override string DisplayText => "Stripe Payment Failed";

    protected override string Description => "Starts when Stripe reports a failed payment.";
}

/// <summary>
/// Describes the <c>StripePaymentCanceledEvent</c> workflow event.
/// </summary>
internal sealed class StripePaymentCanceledEventSchema : StripeEventSchemaBase
{
    public override string Name => StripeWorkflowEventNames.PaymentCanceled;

    protected override string DisplayText => "Stripe Payment Canceled";

    protected override string Description => "Starts when Stripe reports a canceled payment.";
}

/// <summary>
/// Describes the <c>StripePaymentRefundedEvent</c> workflow event.
/// </summary>
internal sealed class StripePaymentRefundedEventSchema : StripeEventSchemaBase
{
    public override string Name => StripeWorkflowEventNames.PaymentRefunded;

    protected override string DisplayText => "Stripe Payment Refunded";

    protected override string Description => "Starts when Stripe reports a refunded payment.";
}

/// <summary>
/// Describes the <c>StripeDisputeCreatedEvent</c> workflow event.
/// </summary>
internal sealed class StripeDisputeCreatedEventSchema : StripeEventSchemaBase
{
    public override string Name => StripeWorkflowEventNames.DisputeCreated;

    protected override string DisplayText => "Stripe Dispute Created";

    protected override string Description => "Starts when Stripe reports a new dispute.";
}

/// <summary>
/// Describes the <c>StripeRequestFailedEvent</c> workflow event.
/// </summary>
internal sealed class StripeRequestFailedEventSchema : StripeEventSchemaBase
{
    public override string Name => StripeWorkflowEventNames.RequestFailed;

    protected override string DisplayText => "Stripe Request Failed";

    protected override string Description => "Starts when a request to Stripe fails.";
}
