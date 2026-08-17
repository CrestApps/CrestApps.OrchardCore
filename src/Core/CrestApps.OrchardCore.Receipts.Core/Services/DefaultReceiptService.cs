using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Receipts.Services;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Receipts.Core.Services;

/// <summary>
/// The default <see cref="IReceiptService"/>. It merges the tenant's configured <see cref="ReceiptSettings"/>
/// branding into the consumer-supplied purchase data and computes the subtotal, producing a printable
/// <see cref="ReceiptDocument"/>.
/// </summary>
public sealed class DefaultReceiptService : IReceiptService
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultReceiptService"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read the receipt settings and the site name fallback.</param>
    public DefaultReceiptService(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public async ValueTask<ReceiptDocument> BuildAsync(ReceiptRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var site = await _siteService.GetSiteSettingsAsync();
        var settings = site.GetOrCreate<ReceiptSettings>();

        var businessName = string.IsNullOrWhiteSpace(settings.BusinessName)
            ? site.SiteName
            : settings.BusinessName;

        return new ReceiptDocument
        {
            HeaderTitle = settings.HeaderTitle,
            BusinessName = businessName,
            LogoUrl = settings.LogoUrl,
            BusinessAddress = settings.BusinessAddress,
            ContactEmail = settings.ContactEmail,
            ContactPhone = settings.ContactPhone,
            Website = settings.Website,
            FooterText = settings.FooterText,
            ShowTestBadge = settings.ShowTestPaymentBadge,
            BilledToName = request.BilledToName,
            BilledToEmail = request.BilledToEmail,
            Reference = request.Reference,
            SourceLabel = request.SourceLabel,
            IssuedAt = request.IssuedAt,
            Currency = request.Currency,
            LineItems = request.LineItems ?? [],
            Subtotal = request.Total - request.TaxAmount,
            TaxLines = request.TaxLines ?? [],
            TaxAmount = request.TaxAmount,
            Total = request.Total,
            Status = request.Status,
            IsTest = request.IsTest,
            GatewayId = request.GatewayId,
            Notes = request.Notes,
        };
    }
}
