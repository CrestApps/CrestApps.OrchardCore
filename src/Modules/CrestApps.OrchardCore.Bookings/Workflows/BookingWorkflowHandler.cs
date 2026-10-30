using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Bookings.Workflows;

/// <summary>
/// Starts the booking workflow events as bookings are made, confirmed, moved, canceled, completed, missed and expire. The events
/// start once the booking is committed, so a workflow never sees a booking that was rolled back.
/// </summary>
internal sealed class BookingWorkflowHandler : IBookingHandler
{
    public Task SavingAsync(BookingSavingContext context)
    {
        var booking = context.Booking;
        var events = new List<string>();

        if (context.IsNew)
        {
            events.Add(nameof(BookingCreatedEvent));
        }

        if (context.IsNew || context.StatusChanged)
        {
            var statusEvent = booking.Status switch
            {
                BookingStatus.Confirmed => nameof(BookingConfirmedEvent),
                BookingStatus.Canceled => nameof(BookingCanceledEvent),
                BookingStatus.Completed => nameof(BookingCompletedEvent),
                BookingStatus.NoShow => nameof(BookingNoShowEvent),
                BookingStatus.Expired => nameof(BookingExpiredEvent),
                _ => null,
            };

            if (statusEvent is not null)
            {
                events.Add(statusEvent);
            }
        }

        if (!context.IsNew && context.Rescheduled)
        {
            events.Add(nameof(BookingRescheduledEvent));
        }

        if (events.Count == 0 || ShellScope.Current is null)
        {
            return Task.CompletedTask;
        }

        var input = CreateInput(booking, context.PreviousStatus);

        ShellScope.AddDeferredTask(async scope =>
        {
            var workflowManager = scope.ServiceProvider.GetService<IWorkflowManager>();

            if (workflowManager is null)
            {
                return;
            }

            foreach (var name in events)
            {
                try
                {
                    await workflowManager.TriggerEventAsync(name, new Dictionary<string, object>(input), correlationId: booking.ItemId);
                }
                catch (Exception ex)
                {
                    scope.ServiceProvider.GetRequiredService<ILogger<BookingWorkflowHandler>>()
                        .LogError(ex, "The booking workflow event '{Event}' failed for the booking '{BookingId}'.", name, booking.ItemId);
                }
            }
        });

        return Task.CompletedTask;
    }

    public Task DeletingAsync(Booking booking)
        => Task.CompletedTask;

    private static Dictionary<string, object> CreateInput(Booking booking, BookingStatus? previousStatus)
        => new()
        {
            ["Booking"] = booking,
            ["BookingId"] = booking.ItemId,
            ["OrderId"] = booking.OrderId,
            ["OrderNumber"] = booking.OrderNumber,
            ["ContentItemId"] = booking.ContentItemId,
            ["Title"] = booking.Title,
            ["StartsUtc"] = booking.StartsUtc,
            ["EndsUtc"] = booking.EndsUtc,
            ["Status"] = booking.Status.ToString(),
            ["PreviousStatus"] = previousStatus?.ToString(),
            ["CustomerName"] = booking.CustomerName,
            ["CustomerEmail"] = booking.CustomerEmail,
        };
}
