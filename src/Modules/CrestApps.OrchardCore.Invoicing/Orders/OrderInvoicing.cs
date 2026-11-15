using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using OrchardCore.Settings;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.Orders;

/// <summary>
/// With Orders on: an order whose whole balance is put on the ledger (paid on account terms, or later) gets an issued
/// invoice that takes over that debt, numbered and printed like any invoice, instead of a second debt.
/// </summary>
[RequireFeatures(OrderConstants.Features.Orders)]
public sealed class OrderInvoicingStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<OrderInvoiceBuilder>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITransactionEventHandler, OrderBalanceInvoiceHandler>());
    }
}

/// <summary>
/// Hears a ledger debt created for an order and invoices the order once the request has saved it.
/// </summary>
internal sealed class OrderBalanceInvoiceHandler : ITransactionEventHandler
{
    /// <summary>
    /// The name of the deferred work that invoices orders.
    /// </summary>
    public const string WorkName = "invoicing_orders";

    private readonly IServiceProvider _serviceProvider;

    public OrderBalanceInvoiceHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task CreatedAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        if (transaction is null ||
            !string.Equals(transaction.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(transaction.ReferenceId))
        {
            return Task.CompletedTask;
        }

        var transactionId = transaction.ItemId;

        if (ShellScope.Current is null)
        {
            return _serviceProvider.GetRequiredService<OrderInvoiceBuilder>().InvoiceAsync(transactionId, cancellationToken);
        }

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<OrderInvoiceBuilder>().InvoiceAsync(transactionId, CancellationToken.None));

        return Task.CompletedTask;
    }

    public Task StatusChangedAsync(Transaction transaction, TransactionStatus previous, TransactionStatus current, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task CanceledAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task BecameOverdueAsync(Transaction transaction, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>
/// Makes the invoice of an order whose balance is a debt on the ledger: the order's lines, shipping and discounts, taxed
/// by the invoice's tax providers for the order's addresses, billed to whoever owes the debt, issued taking the debt
/// over. When the invoice does not come to what the debt owes (tax worked out differently, a discount the lines can't
/// show), it is left as a draft for staff to correct and issue.
/// </summary>
public sealed class OrderInvoiceBuilder
{
    private readonly ITransactionManager _transactions;
    private readonly IOrderStore _orders;
    private readonly IInvoiceManager _invoices;
    private readonly IDocumentCalculator _calculator;
    private readonly ISiteService _siteService;
    private readonly ISession _session;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public OrderInvoiceBuilder(
        ITransactionManager transactions,
        IOrderStore orders,
        IInvoiceManager invoices,
        IDocumentCalculator calculator,
        ISiteService siteService,
        ISession session,
        IServiceProvider serviceProvider,
        ILogger<OrderInvoiceBuilder> logger,
        IStringLocalizer<OrderInvoiceBuilder> stringLocalizer)
    {
        _transactions = transactions;
        _orders = orders;
        _invoices = invoices;
        _calculator = calculator;
        _siteService = siteService;
        _session = session;
        _serviceProvider = serviceProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Invoices the order of a ledger debt, once. Returns the invoice, issued or left as a draft, or <see langword="null"/>
    /// when the debt is not an order's whole balance.
    /// </summary>
    public async Task<Invoice> InvoiceAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        var transaction = string.IsNullOrEmpty(transactionId) ? null : await _transactions.FindByIdAsync(transactionId, cancellationToken);

        if (transaction is null ||
            !string.Equals(transaction.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) ||
            await _orders.FindByIdAsync(transaction.ReferenceId, cancellationToken) is not { } order)
        {
            return null;
        }

        var existing = await _invoices.PageAsync(1, 1, new InvoiceQuery { SourceType = InvoicingConstants.OrderReferenceType, SourceId = order.ItemId }, cancellationToken);

        if (existing.Entries.FirstOrDefault() is { } made)
        {
            return made;
        }

        // Only a debt for the whole order: part paid by card and part on account would need the payment on the invoice too.
        if (order.Total is not { } total || Math.Abs(total - transaction.TotalAmount) > 0.01m)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Order '{OrderNumber}' was not invoiced: its debt of {Debt} on the ledger is not its whole total.", order.OrderNumber, transaction.TotalAmount);
            }

            return null;
        }

        var invoice = await BuildDraftAsync(order, transaction, cancellationToken);
        var validation = await _invoices.ValidateAsync(invoice, cancellationToken);

        if (!validation.Succeeded)
        {
            _logger.LogWarning("The invoice of order '{OrderNumber}' could not be made: {Errors}", order.OrderNumber, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));

            return null;
        }

        await _invoices.CreateAsync(invoice, cancellationToken);

        // Issued in a unit of work of its own, which reads the draft under its lock.
        await _session.SaveChangesAsync(cancellationToken);

        await using var scope = _serviceProvider.CreateAsyncScope();

        var issued = await scope.ServiceProvider.GetRequiredService<IInvoiceCommandService>().IssueAsync(invoice.ItemId, cancellationToken);

        if (!issued.Succeeded)
        {
            _logger.LogWarning("The invoice of order '{OrderNumber}' was left as a draft: {Errors}", order.OrderNumber, string.Join(" ", issued.Errors));

            return invoice;
        }

        return issued.Invoice;
    }

    /// <summary>
    /// Builds the draft invoice of an order whose balance is the ledger debt.
    /// </summary>
    public async Task<Invoice> BuildDraftAsync(Order order, Transaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(transaction);

        var settings = await _siteService.GetSettingsAsync<InvoicingSettings>();
        var invoice = await _invoices.NewAsync(cancellationToken: cancellationToken);
        var payer = transaction.GetPayer();

        invoice.SourceType = InvoicingConstants.OrderReferenceType;
        invoice.SourceId = order.ItemId;
        invoice.TransactionId = transaction.ItemId;
        invoice.Currency = order.Currency ?? transaction.Currency;
        invoice.Reference = order.OrderNumber;
        invoice.PurchaseOrderNumber = order.PurchaseOrderNumber;
        invoice.PricesIncludeTax = order.TaxLines.Any(line => line.IncludedInPrice);
        invoice.AllowPartialPayment = settings.AllowPartialPayment;
        invoice.Culture = order.Culture;
        invoice.BillTo = new DocumentParty
        {
            Party = !payer.IsEmpty ? payer : !string.IsNullOrEmpty(order.UserId) ? new PartyRef(PartyKinds.User, order.UserId) : default,
            Contact = new ContactInfo
            {
                Name = order.BillingAddress?.Name ?? order.CustomerName,
                Email = order.CustomerEmail,
                Address = order.BillingAddress,
            },
            ShippingAddress = order.ShippingAddress,
        };

        if (transaction.DueUtc is { } dueUtc && _serviceProvider.GetService<ILocalClock>() is { } localClock)
        {
            invoice.DueDate = DateOnly.FromDateTime((await localClock.ConvertToLocalAsync(DateTime.SpecifyKind(dueUtc, DateTimeKind.Utc))).DateTime);
        }

        foreach (var line in order.Lines.Where(line => line.Quantity > 0))
        {
            invoice.Lines.Add(new DocumentLine
            {
                Id = IdGenerator.GenerateId(),
                Kind = line.ProductType == ProductType.Service ? DocumentLineKind.Service : DocumentLineKind.Item,
                ProductId = line.ContentItemId,
                Sku = line.Sku,
                Name = string.IsNullOrEmpty(line.VariantTitle) ? line.Title : S["{0} ({1})", line.Title, line.VariantTitle].Value,
                Quantity = line.Quantity,

                // The line's total over its quantity, so add-ons priced into the line are kept.
                UnitPrice = line.LineTotal > 0m ? Math.Round(line.LineTotal / line.Quantity, 6) : line.UnitPrice,
                TaxCategoryCode = line.TaxCategoryCode,
                SourceType = InvoicingConstants.OrderReferenceType,
                SourceId = line.LineId,
            });
        }

        if (order.Shipping is { Amount: > 0m } shipping)
        {
            invoice.Lines.Add(new DocumentLine
            {
                Id = IdGenerator.GenerateId(),
                Kind = DocumentLineKind.Fee,
                Name = string.IsNullOrEmpty(shipping.Name) ? S["Shipping"].Value : S["Shipping: {0}", shipping.Name].Value,
                Quantity = 1m,
                UnitPrice = shipping.Amount,
            });
        }

        foreach (var discount in order.Discounts.Where(discount => discount.Amount > 0m))
        {
            invoice.Lines.Add(new DocumentLine
            {
                Id = IdGenerator.GenerateId(),
                Kind = DocumentLineKind.Fee,
                Name = string.IsNullOrEmpty(discount.Code)
                    ? discount.Description ?? S["Discount"].Value
                    : S["Discount {0}", discount.Code].Value,
                Quantity = 1m,
                UnitPrice = -discount.Amount,
                TaxTreatment = TaxTreatment.OutOfScope,
                TaxTreatmentReason = S["Discount"].Value,
            });
        }

        await InvoiceCalculation.ApplyAsync(_calculator, invoice, settings, issueDate: null, cancellationToken);

        return invoice;
    }
}
