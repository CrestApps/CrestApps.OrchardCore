using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Sets the taxes of a document's lines from the Taxation module's rules, for the bill-to's tax profile (their tax number,
/// exemption and address, from <see cref="IPartyTaxProfileService"/>). Registered only while Taxation is enabled, so
/// Invoicing works without it, with the taxes typed on the lines. Charges keep the taxes typed on them.
/// </summary>
internal sealed class TaxationDocumentTaxProvider : IDocumentTaxProvider
{
    private readonly ITaxService _taxService;
    private readonly IPartyTaxProfileService _partyTaxProfiles;
    private readonly IClock _clock;

    public TaxationDocumentTaxProvider(ITaxService taxService, IPartyTaxProfileService partyTaxProfiles, IClock clock)
    {
        _taxService = taxService;
        _partyTaxProfiles = partyTaxProfiles;
        _clock = clock;
    }

    public int Order => 0;

    public async Task<bool> ApplyTaxesAsync(DocumentCalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var lines = (context.Lines ?? [])
            .Where(line => line is not null && line.Kind is not DocumentLineKind.Text and not DocumentLineKind.Section && !string.IsNullOrEmpty(line.Id))
            .ToList();

        if (lines.Count == 0)
        {
            return false;
        }

        var party = context.BillTo?.Party ?? default;
        var request = new TaxCalculationContext
        {
            Customer = party.IsEmpty ? null : await _partyTaxProfiles.GetAsync(party, cancellationToken),
            Destination = context.BillTo?.ShippingAddress ?? context.BillTo?.Contact?.Address,
            TransactionDateUtc = (context.IssueDate ?? DateOnly.FromDateTime(_clock.UtcNow)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            Currency = context.Currency,
            DefaultPriceType = context.PricesIncludeTax ? TaxPriceType.Inclusive : TaxPriceType.Exclusive,
        };

        foreach (var line in lines)
        {
            request.Items.Add(new TaxableItem
            {
                Id = line.Id,
                Kind = line.Kind is DocumentLineKind.Service or DocumentLineKind.Time ? TaxableItemKind.Service : TaxableItemKind.Physical,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                DiscountAmount = GetDiscount(line),
                Currency = context.Currency,
                TaxCategoryCode = line.TaxCategoryCode,
                PriceIncludesTax = context.PricesIncludeTax,
            });
        }

        var result = await _taxService.CalculateAsync(request, cancellationToken);

        if (result is null)
        {
            return false;
        }

        foreach (var line in lines)
        {
            line.Taxes = [.. result.GetLinesForItem(line.Id).Select(tax => new DocumentLineTax
            {
                Treatment = tax.Treatment,
                TreatmentReason = tax.TreatmentReason,
                Code = tax.TaxCode,
                Name = tax.TaxName,

                // Taxation keeps a rate as a fraction; a document keeps it in percent.
                Rate = tax.Rate * 100m,
                IsCompound = tax.IsCompound,
            })];
        }

        return true;
    }

    private static decimal GetDiscount(DocumentLine line)
    {
        var gross = line.Quantity * line.UnitPrice;

        if (line.DiscountAmount is decimal amount)
        {
            return Math.Clamp(amount, 0m, Math.Abs(gross));
        }

        return line.DiscountPercent is decimal percent
            ? Math.Abs(gross) * Math.Clamp(percent, 0m, 100m) / 100m
            : 0m;
    }
}
