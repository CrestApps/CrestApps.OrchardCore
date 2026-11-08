using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Bookings.Recipes.Workflows;

/// <summary>
/// Describes the booking workflow events, which have no settings of their own.
/// </summary>
internal abstract class BookingEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Commerce";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}

internal sealed class BookingCreatedEventSchema : BookingEventSchemaBase
{
    public override string Name => "BookingCreatedEvent";

    protected override string DisplayText => "Booking Created";

    protected override string Description => "Starts when a booking is made";
}

internal sealed class BookingConfirmedEventSchema : BookingEventSchemaBase
{
    public override string Name => "BookingConfirmedEvent";

    protected override string DisplayText => "Booking Confirmed";

    protected override string Description => "Starts when a booking is confirmed";
}

internal sealed class BookingRescheduledEventSchema : BookingEventSchemaBase
{
    public override string Name => "BookingRescheduledEvent";

    protected override string DisplayText => "Booking Rescheduled";

    protected override string Description => "Starts when a booking moves to another date";
}

internal sealed class BookingCanceledEventSchema : BookingEventSchemaBase
{
    public override string Name => "BookingCanceledEvent";

    protected override string DisplayText => "Booking Canceled";

    protected override string Description => "Starts when a booking is canceled";
}

internal sealed class BookingCompletedEventSchema : BookingEventSchemaBase
{
    public override string Name => "BookingCompletedEvent";

    protected override string DisplayText => "Booking Completed";

    protected override string Description => "Starts when a booking is completed";
}

internal sealed class BookingNoShowEventSchema : BookingEventSchemaBase
{
    public override string Name => "BookingNoShowEvent";

    protected override string DisplayText => "Booking No-Show";

    protected override string Description => "Starts when a booking's guest is marked as not having come";
}

internal sealed class BookingExpiredEventSchema : BookingEventSchemaBase
{
    public override string Name => "BookingExpiredEvent";

    protected override string DisplayText => "Booking Expired";

    protected override string Description => "Starts when a booking expires";
}

internal sealed class BookingPaymentReceivedEventSchema : BookingEventSchemaBase
{
    public override string Name => "BookingPaymentReceivedEvent";

    protected override string DisplayText => "Booking Payment Received";

    protected override string Description => "Starts when a deposit or balance of a booking is paid";
}

internal sealed class BookingPaymentFailedEventSchema : BookingEventSchemaBase
{
    public override string Name => "BookingPaymentFailedEvent";

    protected override string DisplayText => "Booking Payment Failed";

    protected override string Description => "Starts when a charge for a booking fails, such as its balance";
}

internal sealed class BookingRefundedEventSchema : BookingEventSchemaBase
{
    public override string Name => "BookingRefundedEvent";

    protected override string DisplayText => "Booking Refunded";

    protected override string Description => "Starts when money paid for a booking is refunded";
}
