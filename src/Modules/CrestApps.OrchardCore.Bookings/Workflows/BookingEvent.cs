using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Bookings.Workflows;

/// <summary>
/// A workflow event about a booking. The input carries <c>Booking</c>, <c>BookingId</c>, <c>OrderId</c>, <c>OrderNumber</c>,
/// <c>ContentItemId</c>, <c>Title</c>, <c>StartsUtc</c>, <c>EndsUtc</c>, <c>Status</c>, <c>PreviousStatus</c>,
/// <c>CustomerName</c> and <c>CustomerEmail</c>.
/// </summary>
public abstract class BookingEvent : EventActivity
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected BookingEvent(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override LocalizedString Category => S["Commerce"];

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc />
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    /// <inheritdoc />
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}
