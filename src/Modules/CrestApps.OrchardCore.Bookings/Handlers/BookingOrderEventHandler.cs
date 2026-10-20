using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Bookings.Handlers;

/// <summary>
/// Books an order's booking lines when it's placed, holding their dates, confirms them when it's paid, and gives their
/// dates back when it's canceled.
/// </summary>
internal sealed class BookingOrderEventHandler : OrderEventHandlerBase
{
    // Resolved when an order changes: the booking service adds notes through the order service, which calls this.
    private readonly IServiceProvider _serviceProvider;

    public BookingOrderEventHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private BookingService Bookings
        => _serviceProvider.GetRequiredService<BookingService>();

    public override async Task CreatedAsync(OrderCreatedContext context, CancellationToken cancellationToken = default)
    {
        if (await Bookings.BookAsync(context.Order, cancellationToken) is { } problem)
        {
            context.Refuse(problem);
        }
    }

    public override Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
        => Bookings.ConfirmPaidAsync(context.Order, cancellationToken);

    public override Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default)
        => Bookings.ReleaseOrderAsync(context.Order, BookingStatus.Canceled, cancellationToken);

    public override Task LinesChangedAsync(OrderLinesChangedContext context, CancellationToken cancellationToken = default)
        => Bookings.RebookAsync(context.Order, context.Refuse, cancellationToken);
}
