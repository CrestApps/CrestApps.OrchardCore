using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Share links signed with <see cref="SignedTokenService"/>, valid for <see cref="InvoicingSettings.ShareLinkDays"/>.
/// </summary>
public sealed class InvoiceShareLinkService : IInvoiceShareLinkService
{
    private const string _purpose = "CrestApps.OrchardCore.Invoicing.ShareLink";

    private readonly SignedTokenService _tokens;
    private readonly SiteUrlGenerator _urls;
    private readonly ISiteService _siteService;

    public InvoiceShareLinkService(SignedTokenService tokens, SiteUrlGenerator urls, ISiteService siteService)
    {
        _tokens = tokens;
        _urls = urls;
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public async Task<string> CreateUrlAsync(Invoice invoice, CancellationToken cancellationToken = default)
        // The route's own area, controller and action, so the link is built from any page, another module's included.
        => await _urls.GetUrlAsync(InvoicingConstants.RouteNames.View, new { area = InvoicingConstants.Features.Area, controller = "Invoice", action = "Show", token = await CreateTokenAsync(invoice) });

    /// <summary>
    /// Creates the token of a link to the invoice's public page.
    /// </summary>
    public async Task<string> CreateTokenAsync(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var site = await _siteService.GetSiteSettingsAsync();
        var days = site.GetOrCreate<InvoicingSettings>()?.ShareLinkDays ?? 0;

        return _tokens.Protect(_purpose, invoice.ItemId, TimeSpan.FromDays(Math.Clamp(days <= 0 ? 60 : days, 1, 3650)));
    }

    /// <inheritdoc/>
    public string GetInvoiceId(string token)
        => string.IsNullOrWhiteSpace(token) ? null : _tokens.Unprotect(_purpose, token);
}
