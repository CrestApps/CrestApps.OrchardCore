using CrestApps.OrchardCore.Commerce.Stores;
using CrestApps.OrchardCore.Receipts.Models;
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
    private readonly IStoreLocationService _storeLocations;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultReceiptService"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read the receipt settings and the site name fallback.</param>
    /// <param name="storeLocations">The store's places, which write the store's address when the receipt settings have
    /// none; or <see langword="null"/>.</param>
    public DefaultReceiptService(ISiteService siteService, IStoreLocationService storeLocations = null)
    {
        _siteService = siteService;
        _storeLocations = storeLocations;
    }

    /// <inheritdoc/>
    public async ValueTask<ReceiptDocument> BuildAsync(ReceiptRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var site = await _siteService.GetSiteSettingsAsync();
        var settings = site.GetOrCreate<ReceiptSettings>();

        // Each of the receipt's own settings falls back to the store profile when it's empty.
        var profile = site.GetOrCreate<StoreProfileSettings>() ?? new StoreProfileSettings();
        var businessName = FirstOf(settings.BusinessName, profile.Name, site.SiteName);
        var businessAddress = string.IsNullOrWhiteSpace(settings.BusinessAddress) && _storeLocations is not null
            ? await _storeLocations.GetAddressTextAsync()
            : settings.BusinessAddress;

        return new ReceiptDocument
        {
            HeaderTitle = settings.HeaderTitle,
            BusinessName = businessName,
            LogoUrl = FirstOf(settings.LogoUrl, profile.LogoUrl),
            BusinessAddress = businessAddress,
            ContactEmail = FirstOf(settings.ContactEmail, profile.Email),
            ContactPhone = FirstOf(settings.ContactPhone, profile.Phone),
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

    private static string FirstOf(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
