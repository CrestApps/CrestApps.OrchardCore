using System.Data.Common;
using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Fulfillment.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.Fulfillment.Core.Handlers;

/// <summary>
/// When automatic sending is on, records a request for each connected partner as an order is paid. Nothing is sent
/// here: the request is saved with the payment, and a background task sends it.
/// </summary>
public sealed class FulfillmentOrderEventHandler : OrderEventHandlerBase
{
    private readonly ISiteService _siteService;
    private readonly FulfillmentSender _sender;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FulfillmentOrderEventHandler"/> class.
    /// </summary>
    /// <param name="siteService">The site service, for the fulfillment settings.</param>
    /// <param name="sender">The fulfillment sender.</param>
    /// <param name="logger">The logger.</param>
    public FulfillmentOrderEventHandler(
        ISiteService siteService,
        FulfillmentSender sender,
        ILogger<FulfillmentOrderEventHandler> logger)
    {
        _siteService = siteService;
        _sender = sender;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override async Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var settings = await _siteService.GetSettingsAsync<FulfillmentSettings>();

            if (!settings.SendAutomatically)
            {
                return;
            }

            await _sender.RecordAsync(context.Order, cancellationToken);
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or ConcurrencyException or DbException))
        {
            // Only this module's own failures are swallowed, so they never hold up the payment; the order can still be
            // sent from its page. A database or concurrency failure propagates, so the checkout rolls back and retries.
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(exception, "Order '{OrderNumber}' could not be recorded for the fulfillment partners.", context.Order?.OrderNumber);
            }
        }
    }
}
