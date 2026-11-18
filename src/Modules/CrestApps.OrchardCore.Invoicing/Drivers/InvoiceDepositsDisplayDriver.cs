using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Invoicing.Drivers;

/// <summary>
/// Shows, on a draft invoice's page, the deposit to ask for first and the paid deposits it can deduct; on a deposit
/// invoice, the invoice it is a deposit for.
/// </summary>
public sealed class InvoiceDepositsDisplayDriver : DisplayDriver<Invoice>
{
    private readonly IInvoicePrepaymentService _prepayments;
    private readonly IInvoiceManager _invoices;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public InvoiceDepositsDisplayDriver(
        IInvoicePrepaymentService prepayments,
        IInvoiceManager invoices,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _prepayments = prepayments;
        _invoices = invoices;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    public override IDisplayResult Display(Invoice invoice, BuildDisplayContext context)
    {
        if (!string.Equals(context.DisplayType, InvoicingConstants.DisplayTypes.DetailAdmin, StringComparison.Ordinal) || invoice.Kind == InvoiceKind.DebitNote)
        {
            return null;
        }

        return Initialize<InvoiceDepositsViewModel>("InvoiceDeposits_DetailAdmin", async model =>
        {
            model.Invoice = invoice;

            if (invoice.Kind == InvoiceKind.Prepayment)
            {
                model.DepositFor = string.IsNullOrEmpty(invoice.RelatedInvoiceId) ? null : await _invoices.FindByIdAsync(invoice.RelatedInvoiceId);

                return;
            }

            if (invoice.Status != InvoiceStatus.Draft)
            {
                return;
            }

            model.CanManage = await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, InvoicingPermissions.ManageInvoices);
            model.Deductible = await _prepayments.ListDeductibleAsync(invoice.ItemId);
            model.Requested = [.. (await _invoices.PageAsync(1, 20, new InvoiceQuery { RelatedInvoiceId = invoice.ItemId })).Entries.Where(entry => entry.Kind == InvoiceKind.Prepayment)];
        }).Location(InvoicingConstants.DisplayTypes.DetailAdmin, "Content:7");
    }
}
