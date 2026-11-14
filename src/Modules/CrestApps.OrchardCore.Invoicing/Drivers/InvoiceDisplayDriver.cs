using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Transactions.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Invoicing.Drivers;

/// <summary>
/// Builds an invoice's row in the invoices list and the cards of its page. Another module adds a card to the page with
/// a driver of its own for the <see cref="InvoicingConstants.DisplayTypes.DetailAdmin"/> display type.
/// </summary>
public sealed class InvoiceDisplayDriver : DisplayDriver<Invoice>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IServiceProvider _serviceProvider;

    public InvoiceDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IServiceProvider serviceProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _serviceProvider = serviceProvider;
    }

    public override async Task<IDisplayResult> DisplayAsync(Invoice invoice, BuildDisplayContext context)
    {
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid)
        {
            return await DisplayRowAsync(invoice, context);
        }

        if (context.DisplayType == InvoicingConstants.DisplayTypes.DetailAdmin)
        {
            return await DisplayPageAsync(invoice);
        }

        return null;
    }

    private async Task<IDisplayResult> DisplayRowAsync(Invoice invoice, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        var model = new InvoiceDetailViewModel
        {
            Invoice = invoice,
            BillToName = await GetBillToNameAsync(invoice),
            CanEdit = InvoiceLifecycle.CanEdit(invoice) &&
                await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, InvoicingPermissions.ManageInvoices),
        };

        return Combine(
            View("Invoice_Fields_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Number:1"),
            View("Invoice_Status_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("Invoice_BillTo_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "BillTo:5"),
            View("Invoice_Amount_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Amount:5"),
            View("Invoice_Due_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:15")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Due:5"),
            View("Invoice_Buttons_SummaryAdmin", model).Location("Actions:5"));
    }

    private async Task<IDisplayResult> DisplayPageAsync(Invoice invoice)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var canManage = await _authorizationService.AuthorizeAsync(user, InvoicingPermissions.ManageInvoices);
        var canIssue = await _authorizationService.AuthorizeAsync(user, InvoicingPermissions.IssueInvoices);
        var canVoid = await _authorizationService.AuthorizeAsync(user, InvoicingPermissions.VoidInvoices);

        var model = new InvoiceDetailViewModel
        {
            Invoice = invoice,
            BillToName = await GetBillToNameAsync(invoice),
            CanEdit = canManage && InvoiceLifecycle.CanEdit(invoice),
            CanDelete = canManage && InvoiceLifecycle.CanDelete(invoice),
            CanIssue = canIssue && invoice.Status == InvoiceStatus.Draft,
            CanVoid = canVoid && InvoiceLifecycle.CanVoid(invoice),
            CanWriteOff = canVoid && InvoiceLifecycle.CanWriteOff(invoice),
            CanReinstate = canVoid && invoice.Status == InvoiceStatus.Uncollectible,
            CanOpenTransaction = !string.IsNullOrEmpty(invoice.TransactionId) &&
                await _authorizationService.AuthorizeAsync(user, TransactionsPermissions.ManageTransactions),
        };

        return Combine(
            Card("InvoiceSummary_DetailAdmin", model).Location(InvoicingConstants.DisplayTypes.DetailAdmin, "Content:1"),
            Card("InvoiceLines_DetailAdmin", model).Location(InvoicingConstants.DisplayTypes.DetailAdmin, "Content:5"),
            Card("InvoiceHistory_DetailAdmin", model).Location(InvoicingConstants.DisplayTypes.DetailAdmin, "Content:10"),
            Card("InvoiceActions_DetailAdmin", model).Location(InvoicingConstants.DisplayTypes.DetailAdmin, "Actions:1"));
    }

    // A card of the page, whose view takes the card's model itself.
    private ShapeResult Card(string shapeType, InvoiceDetailViewModel model)
        => Initialize<InvoiceDetailViewModel>(shapeType, card =>
        {
            card.Invoice = model.Invoice;
            card.BillToName = model.BillToName;
            card.CanEdit = model.CanEdit;
            card.CanIssue = model.CanIssue;
            card.CanVoid = model.CanVoid;
            card.CanWriteOff = model.CanWriteOff;
            card.CanReinstate = model.CanReinstate;
            card.CanDelete = model.CanDelete;
            card.CanOpenTransaction = model.CanOpenTransaction;
        });

    // The name typed on the invoice, else the bill-to party's.
    private async Task<string> GetBillToNameAsync(Invoice invoice)
    {
        if (!string.IsNullOrWhiteSpace(invoice.BillTo?.Contact?.Name))
        {
            return invoice.BillTo.Contact.Name;
        }

        if (invoice.BillTo is { Party.IsEmpty: false } billTo && _serviceProvider.GetService<IPartyDirectory>() is { } parties)
        {
            return await parties.GetNameAsync(billTo.Party);
        }

        return invoice.BillTo?.Contact?.Email;
    }
}
