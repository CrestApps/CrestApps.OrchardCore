using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Bookings.Workflows;

/// <summary>
/// Starts the booking payment workflow events, one for each booking the money is for, once the payment, the failed charge
/// or the refund is committed, so a workflow never sees one that was rolled back.
/// </summary>
internal sealed class BookingPaymentWorkflowHandler : IBookingPaymentHandler
{
    public Task PaymentReceivedAsync(BookingPaymentContext context, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(BookingPaymentReceivedEvent), context);

    public Task PaymentFailedAsync(BookingPaymentContext context, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(BookingPaymentFailedEvent), context);

    public Task RefundedAsync(BookingPaymentContext context, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(BookingRefundedEvent), context);

    internal static Dictionary<string, object> CreateInput(Booking booking, BookingPaymentContext context)
    {
        var input = BookingWorkflowHandler.CreateInput(booking, previousStatus: null);

        input["Amount"] = context.Amount;
        input["Currency"] = context.Currency;
        input["PaymentKind"] = context.PaymentKind?.ToString();
        input["Reason"] = context.Reason;
        input["IsFinal"] = context.IsFinal;
        input["PaymentReference"] = context.Reference;

        return input;
    }

    private static Task TriggerAsync(string name, BookingPaymentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Bookings is not { Count: > 0 } bookings || ShellScope.Current is null)
        {
            return Task.CompletedTask;
        }

        // The input is read now, while the bookings are as this payment left them.
        var inputs = bookings.Select(booking => (booking.ItemId, Input: CreateInput(booking, context))).ToList();

        ShellScope.AddDeferredTask(async scope =>
        {
            var workflowManager = scope.ServiceProvider.GetService<IWorkflowManager>();

            if (workflowManager is null)
            {
                return;
            }

            foreach (var (bookingId, input) in inputs)
            {
                try
                {
                    await workflowManager.TriggerEventAsync(name, input, correlationId: bookingId);
                }
                catch (Exception ex)
                {
                    scope.ServiceProvider.GetRequiredService<ILogger<BookingPaymentWorkflowHandler>>()
                        .LogError(ex, "The booking workflow event '{Event}' failed for the booking '{BookingId}'.", name, bookingId);
                }
            }
        });

        return Task.CompletedTask;
    }
}
