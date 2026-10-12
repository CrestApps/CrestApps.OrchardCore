using CrestApps.OrchardCore.Stripe.Workflows.Events;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Stripe.Workflows.Drivers;

/// <summary>
/// Provides the default admin display shapes for the Stripe workflow events.
/// </summary>
/// <typeparam name="TActivity">The Stripe workflow event type.</typeparam>
public sealed class StripeEventActivityDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity>
    where TActivity : StripeEventActivity
{
}
