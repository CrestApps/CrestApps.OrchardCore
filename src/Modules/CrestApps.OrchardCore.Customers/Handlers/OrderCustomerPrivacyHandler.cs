using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Customers.Handlers;

/// <summary>
/// Erases the personal details of a customer's orders with the customer, and adds the orders and the transactions
/// paying for them to the customer's export.
/// </summary>
public sealed class OrderCustomerPrivacyHandler : ICustomerPrivacyHandler
{
    private const int MaxOrders = 10_000;
    private const int PageSize = 100;

    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    private readonly IOrderStore _orderStore;
    private readonly OrderAnonymizer _anonymizer;
    private readonly IServiceProvider _serviceProvider;

    public OrderCustomerPrivacyHandler(IOrderStore orderStore, OrderAnonymizer anonymizer, IServiceProvider serviceProvider)
    {
        _orderStore = orderStore;
        _anonymizer = anonymizer;
        _serviceProvider = serviceProvider;
    }

    public async Task ErasingAsync(CustomerPrivacyContext context, CancellationToken cancellationToken = default)
    {
        await _anonymizer.AnonymizeAsync(new OrderQuery { CustomerId = context.Customer.ItemId }, MaxOrders, cancellationToken);

        // An account holder's orders include those from before the customer record existed.
        if (!string.IsNullOrEmpty(context.UserId))
        {
            await _anonymizer.AnonymizeAsync(new OrderQuery { UserId = context.UserId }, MaxOrders, cancellationToken);
        }
    }

    public async Task ExportingAsync(CustomerExportContext context, CancellationToken cancellationToken = default)
    {
        var orders = new Dictionary<string, Order>(StringComparer.Ordinal);

        await CollectAsync(new OrderQuery { CustomerId = context.Customer.ItemId }, orders, cancellationToken);

        if (!string.IsNullOrEmpty(context.UserId))
        {
            await CollectAsync(new OrderQuery { UserId = context.UserId }, orders, cancellationToken);
        }

        var transactions = _serviceProvider.GetService<ITransactionManager>();
        var exported = new JsonArray();

        foreach (var order in orders.Values.OrderBy(order => order.CreatedUtc))
        {
            var node = JsonSerializer.SerializeToNode(new
            {
                order.ItemId,
                order.OrderNumber,
                Status = order.Status.ToString(),
                order.CreatedUtc,
                order.PaidUtc,
                order.Currency,
                order.Subtotal,
                order.TaxAmount,
                order.Total,
                order.CustomerName,
                order.CustomerEmail,
                order.CustomerPhone,
                order.BillingAddress,
                Lines = order.Lines.Select(line => new { line.Title, line.VariantTitle, line.Sku, line.Quantity, line.UnitPrice, line.LineTotal, line.AddOns }),
            }, _options);

            if (transactions is not null)
            {
                var result = await transactions.PageAsync(1, PageSize, new TransactionQuery
                {
                    ReferenceType = CheckoutReferenceTypes.Order,
                    ReferenceId = order.ItemId,
                }, cancellationToken);

                node["transactions"] = JsonSerializer.SerializeToNode(result.Entries.Select(transaction => new
                {
                    transaction.ItemId,
                    transaction.Title,
                    transaction.InvoiceNumber,
                    Status = transaction.Status.ToString(),
                    transaction.Currency,
                    transaction.TotalAmount,
                    transaction.AmountPaid,
                    transaction.AmountRefunded,
                    transaction.CreatedUtc,
                    transaction.SettledUtc,
                    transaction.GuestContactName,
                    transaction.GuestContactEmail,
                }), _options);
            }

            exported.Add(node);
        }

        context.Export["orders"] = exported;
    }

    private async Task CollectAsync(OrderQuery query, Dictionary<string, Order> orders, CancellationToken cancellationToken)
    {
        for (var skip = 0; skip < MaxOrders; skip += PageSize)
        {
            var (_, page) = await _orderStore.PageAsync(query, skip, PageSize, cancellationToken);

            foreach (var order in page)
            {
                orders.TryAdd(order.ItemId, order);
            }

            if (page.Count < PageSize)
            {
                break;
            }
        }
    }
}
