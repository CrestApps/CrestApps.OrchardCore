using CrestApps.Core.AI;
using CrestApps.OrchardCore.Bookings.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Bookings;

/// <summary>
/// Registers the bookings AI tools when the AI feature is on: listing and reading bookings, finishing, canceling (with a
/// refund) and charging the balance, and the dates of bookable products. The two money tools ask for approval and need an
/// amount, its currency and an idempotency key.
/// </summary>
[Feature(BookingsConstants.Feature)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListBookingsTool>(ListBookingsTool.TheName)
            .WithTitle(S["List Bookings"])
            .WithDescription(S["Lists bookings with optional search, status, product, date range and paging; by default only bookings from today on. Returns title, date, people, customer, total, status and order number."])
            .WithCategory(S["Bookings"])
            .Selectable();

        services.AddCoreAITool<GetBookingTool>(GetBookingTool.TheName)
            .WithTitle(S["Get Booking"])
            .WithDescription(S["Returns one booking with its customer, travelers count, the balance still owed and when it is due, and the cancellation quote (fee percent, amount paid, refund the policy gives). Use it before canceling or charging."])
            .WithCategory(S["Bookings"])
            .Selectable();

        services.AddCoreAITool<CompleteBookingTool>(CompleteBookingTool.TheName)
            .WithTitle(S["Complete Booking"])
            .WithDescription(S["Marks a confirmed booking as completed, or as a no-show. Only a confirmed booking can be finished."])
            .WithCategory(S["Bookings"])
            .Selectable();

        services.AddCoreAITool<CancelBookingTool>(CancelBookingTool.TheName)
            .WithTitle(S["Cancel Booking"])
            .WithDescription(S["Cancels a booking, refunds the buyer the stated amount and currency (0 for no refund, refundAll for everything paid), cancels any balance still owed, frees the dates and emails the buyer. Every call needs a new idempotency key; a repeated key is refused. Cannot be undone."])
            .WithCategory(S["Bookings"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<ChargeBookingBalanceTool>(ChargeBookingBalanceTool.TheName)
            .WithTitle(S["Charge Booking Balance"])
            .WithDescription(S["Charges the balance still owed on a booking to the card its deposit was paid with, now, including after an earlier failed attempt. The amount and currency must equal the balance owed, and every call needs a new idempotency key. Fails when nothing is owed."])
            .WithCategory(S["Bookings"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<ListBookingDatesTool>(ListBookingDatesTool.TheName)
            .WithTitle(S["List Booking Dates"])
            .WithDescription(S["Lists the dates or time slots of one bookable product, or of every product in a date range (the calendar view): capacity, held, sold, remaining, price override, closed."])
            .WithCategory(S["Bookings"])
            .Selectable();

        services.AddCoreAITool<SaveBookingDateTool>(SaveBookingDateTool.TheName)
            .WithTitle(S["Save Booking Date"])
            .WithDescription(S["Adds a date or time slot to a bookable product with a capacity and optional price, or changes an existing date's capacity, price or closed flag. Capacity cannot go below what is already booked; a date is closed, never deleted."])
            .WithCategory(S["Bookings"])
            .Selectable();
    }
}
