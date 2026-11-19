using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// The default <see cref="IInvoicePrepaymentService"/>.
/// </summary>
public sealed class DefaultInvoicePrepaymentService : IInvoicePrepaymentService
{
    private readonly IInvoiceManager _invoices;
    private readonly IDocumentCalculator _calculator;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public DefaultInvoicePrepaymentService(
        IInvoiceManager invoices,
        IDocumentCalculator calculator,
        ISiteService siteService,
        IStringLocalizer<DefaultInvoicePrepaymentService> stringLocalizer)
    {
        _invoices = invoices;
        _calculator = calculator;
        _siteService = siteService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<InvoiceCommandResult> RequestDepositAsync(string invoiceId, decimal? percent, decimal? amount, CancellationToken cancellationToken = default)
    {
        var result = new InvoiceCommandResult();
        var invoice = string.IsNullOrEmpty(invoiceId) ? null : await _invoices.FindByIdAsync(invoiceId, cancellationToken);

        if (invoice is null)
        {
            return result.Fail(S["The invoice could not be found."]);
        }

        if (invoice.Status != InvoiceStatus.Draft || invoice.Kind != InvoiceKind.Standard)
        {
            return result.Fail(S["A deposit can only be asked for on a draft invoice."]);
        }

        var settings = await _siteService.GetSettingsAsync<InvoicingSettings>();

        // The draft's totals as the calculator works them out now; the draft itself is not saved.
        await InvoiceCalculation.ApplyAsync(_calculator, invoice, settings, issueDate: null, cancellationToken);

        var total = invoice.Totals?.Total ?? 0m;
        var deposit = percent is > 0m and <= 100m
            ? Math.Round(total * percent.Value / 100m, 2, MidpointRounding.AwayFromZero)
            : amount is > 0m ? amount.Value : 0m;

        if (deposit <= 0m || deposit > total)
        {
            return result.Fail(S["Enter a percent from 0 to 100, or an amount up to the invoice's total."]);
        }

        var prepayment = await _invoices.NewAsync(cancellationToken: cancellationToken);

        prepayment.Kind = InvoiceKind.Prepayment;
        prepayment.RelatedInvoiceId = invoice.ItemId;
        prepayment.BillTo = CopyParty(invoice.BillTo);
        prepayment.Currency = invoice.Currency;
        prepayment.PricesIncludeTax = invoice.PricesIncludeTax;
        prepayment.PurchaseOrderNumber = invoice.PurchaseOrderNumber;
        prepayment.Reference = invoice.Reference;
        prepayment.SourceType = invoice.SourceType;
        prepayment.SourceId = invoice.SourceId;
        prepayment.PaymentTermId = invoice.PaymentTermId;
        prepayment.AllowPartialPayment = invoice.AllowPartialPayment;
        prepayment.TemplateId = invoice.TemplateId;
        prepayment.Lines.Add(new DocumentLine
        {
            Id = IdGenerator.GenerateId(),
            Kind = DocumentLineKind.Deposit,
            Name = percent is > 0m
                ? S["Deposit of {0}% before the work", percent.Value.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture)].Value
                : S["Deposit before the work"].Value,
            Quantity = 1m,
            UnitPrice = deposit,
            TaxTreatment = TaxTreatment.OutOfScope,
            TaxTreatmentReason = S["Deposit; taxed on the final invoice"].Value,
            SourceType = InvoicingConstants.InvoiceReferenceType,
            SourceId = invoice.ItemId,
        });

        await InvoiceCalculation.ApplyAsync(_calculator, prepayment, settings, issueDate: null, cancellationToken);

        var validation = await _invoices.ValidateAsync(prepayment, cancellationToken);

        if (!validation.Succeeded)
        {
            foreach (var error in validation.Errors)
            {
                result.Fail(error.ErrorMessage);
            }

            return result;
        }

        await _invoices.CreateAsync(prepayment, cancellationToken);
        result.Invoice = prepayment;

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Invoice>> ListDeductibleAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = string.IsNullOrEmpty(invoiceId) ? null : await _invoices.FindByIdAsync(invoiceId, cancellationToken);

        if (invoice is not { Status: InvoiceStatus.Draft } || invoice.Kind == InvoiceKind.Prepayment)
        {
            return [];
        }

        var candidates = new Dictionary<string, Invoice>(StringComparer.Ordinal);

        foreach (var linked in (await _invoices.PageAsync(1, 100, new InvoiceQuery { RelatedInvoiceId = invoice.ItemId }, cancellationToken)).Entries)
        {
            candidates[linked.ItemId] = linked;
        }

        if (invoice.BillTo?.Party is { IsEmpty: false } party)
        {
            var billed = await _invoices.PageAsync(1, 200, new InvoiceQuery { BillTo = [party], OnlyBillTo = true, IssuedOnly = true }, cancellationToken);

            foreach (var other in billed.Entries)
            {
                candidates.TryAdd(other.ItemId, other);
            }
        }

        var deductible = new List<Invoice>();

        foreach (var prepayment in candidates.Values.Where(IsPaidPrepayment))
        {
            if (!string.Equals(prepayment.Currency, invoice.Currency, StringComparison.OrdinalIgnoreCase) ||
                invoice.Lines.Any(line => IsDeductionOf(line, prepayment)) ||
                await IsDeductedElsewhereAsync(prepayment, invoice.ItemId, cancellationToken))
            {
                continue;
            }

            deductible.Add(prepayment);
        }

        return [.. deductible.OrderBy(prepayment => prepayment.IssuedUtc)];
    }

    /// <inheritdoc/>
    public async Task<InvoiceCommandResult> DeductAsync(string invoiceId, string prepaymentInvoiceId, CancellationToken cancellationToken = default)
    {
        var result = new InvoiceCommandResult();
        var invoice = string.IsNullOrEmpty(invoiceId) ? null : await _invoices.FindByIdAsync(invoiceId, cancellationToken);

        if (invoice is null)
        {
            return result.Fail(S["The invoice could not be found."]);
        }

        result.Invoice = invoice;

        if (!(await ListDeductibleAsync(invoiceId, cancellationToken)).Any(candidate => string.Equals(candidate.ItemId, prepaymentInvoiceId, StringComparison.Ordinal)))
        {
            return result.Fail(S["That deposit can't be deducted from this invoice: it is not paid, is in another currency, or was deducted already."]);
        }

        var prepayment = await _invoices.FindByIdAsync(prepaymentInvoiceId, cancellationToken);
        var settings = await _siteService.GetSettingsAsync<InvoicingSettings>();

        await InvoiceCalculation.ApplyAsync(_calculator, invoice, settings, issueDate: null, cancellationToken);

        if (!InvoicePrepayments.AddDeduction(invoice, prepayment, S))
        {
            return result.Fail(S["Nothing is left on this invoice to deduct the deposit from."]);
        }

        await InvoiceCalculation.ApplyAsync(_calculator, invoice, settings, issueDate: null, cancellationToken);
        await _invoices.UpdateAsync(invoice, cancellationToken: cancellationToken);

        return result;
    }

    private static bool IsPaidPrepayment(Invoice invoice)
        => invoice is { Kind: InvoiceKind.Prepayment, Status: InvoiceStatus.Open or InvoiceStatus.Uncollectible } && invoice.AmountPaid > 0m;

    // Matched by its source alone, so a line whose kind was changed in the editor still counts.
    private static bool IsDeductionOf(DocumentLine line, Invoice prepayment)
        => string.Equals(line.SourceType, InvoicePrepayments.SourceType, StringComparison.Ordinal) &&
            string.Equals(line.SourceId, prepayment.ItemId, StringComparison.Ordinal);

    // Deducted by another invoice that is not void: one of the same customer, the invoice it was asked from, or one made
    // from the same estimate.
    private async Task<bool> IsDeductedElsewhereAsync(Invoice prepayment, string exceptInvoiceId, CancellationToken cancellationToken)
    {
        var others = new List<Invoice>();

        if (prepayment.BillTo?.Party is { IsEmpty: false } party)
        {
            others.AddRange((await _invoices.PageAsync(1, 500, new InvoiceQuery { BillTo = [party], OnlyBillTo = true }, cancellationToken)).Entries);
        }

        if (!string.IsNullOrEmpty(prepayment.RelatedInvoiceId) && await _invoices.FindByIdAsync(prepayment.RelatedInvoiceId, cancellationToken) is { } related)
        {
            others.Add(related);
        }

        if (!string.IsNullOrEmpty(prepayment.SourceType) && !string.IsNullOrEmpty(prepayment.SourceId))
        {
            others.AddRange((await _invoices.PageAsync(1, 100, new InvoiceQuery { SourceType = prepayment.SourceType, SourceId = prepayment.SourceId }, cancellationToken)).Entries);
        }

        return others.Any(other =>
            !string.Equals(other.ItemId, exceptInvoiceId, StringComparison.Ordinal) &&
            other.Status != InvoiceStatus.Void &&
            other.Lines.Any(line => IsDeductionOf(line, prepayment)));
    }

    private static DocumentParty CopyParty(DocumentParty party)
        => party is null ? new DocumentParty() : new DocumentParty
        {
            Party = party.Party,
            Contact = party.Contact,
            ShippingAddress = party.ShippingAddress,
            TaxId = party.TaxId,
            TaxTreatment = party.TaxTreatment,
            ExemptionReason = party.ExemptionReason,
            CopyTo = [.. party.CopyTo ?? []],
        };
}

/// <summary>
/// The deduction of a paid prepayment invoice on the final invoice, shared by the prepayment service and the estimate
/// conversion.
/// </summary>
public static class InvoicePrepayments
{
    /// <summary>
    /// The source type of a line that deducts a prepayment invoice; its source id is that invoice's id.
    /// </summary>
    public const string SourceType = "Prepayment";

    /// <summary>
    /// Adds the line that deducts what was paid on a prepayment invoice, at most what the invoice comes to before it.
    /// Returns <see langword="false"/> when nothing is left to deduct from.
    /// </summary>
    /// <param name="invoice">The final invoice, a draft.</param>
    /// <param name="prepayment">The paid prepayment invoice.</param>
    /// <param name="S">The localizer of the line's text.</param>
    public static bool AddDeduction(Invoice invoice, Invoice prepayment, IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(prepayment);
        ArgumentNullException.ThrowIfNull(S);

        var before = invoice.Totals?.Total ?? 0m;
        var amount = Math.Min(prepayment.AmountPaid, before);

        if (amount <= 0m)
        {
            return false;
        }

        invoice.Lines.Add(new DocumentLine
        {
            Id = IdGenerator.GenerateId(),
            Kind = DocumentLineKind.Deposit,
            Name = S["Less the deposit paid on invoice {0}", prepayment.Number].Value,
            Quantity = 1m,
            UnitPrice = -amount,
            TaxTreatment = TaxTreatment.OutOfScope,
            TaxTreatmentReason = S["Deposit paid"].Value,
            SourceType = SourceType,
            SourceId = prepayment.ItemId,
        });

        return true;
    }
}
