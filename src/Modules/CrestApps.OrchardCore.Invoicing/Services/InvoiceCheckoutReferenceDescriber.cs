using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Names the invoice a transaction was posted for, with a link to its page, wherever a transaction shows what it is
/// for (the transaction's page, the payments list).
/// </summary>
public sealed class InvoiceCheckoutReferenceDescriber : ICheckoutReferenceDescriber
{
    private readonly IInvoiceStore _invoiceStore;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public InvoiceCheckoutReferenceDescriber(
        IInvoiceStore invoiceStore,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<InvoiceCheckoutReferenceDescriber> stringLocalizer)
    {
        _invoiceStore = invoiceStore;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public async Task<CheckoutReferenceDescription> DescribeAsync(string referenceType, string referenceId, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(referenceType, InvoicingConstants.InvoiceReferenceType, StringComparison.Ordinal) || string.IsNullOrEmpty(referenceId))
        {
            return null;
        }

        var invoice = await _invoiceStore.FindByIdAsync(referenceId, cancellationToken);

        if (invoice is null)
        {
            return null;
        }

        var httpContext = _httpContextAccessor.HttpContext;

        return new CheckoutReferenceDescription
        {
            Text = string.IsNullOrEmpty(invoice.Number) ? S["Draft invoice"] : S["Invoice {0}", invoice.Number],
            Url = httpContext is null ? null : _linkGenerator.GetPathByName(httpContext, "InvoicesDetail", new { itemId = invoice.ItemId }),
        };
    }
}
