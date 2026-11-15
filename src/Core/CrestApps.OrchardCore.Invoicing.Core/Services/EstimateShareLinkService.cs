using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Signed links to an estimate's public page, where the customer reads it and accepts or declines it. They last as long
/// as the estimate can be accepted, and at least as long as the invoicing share links.
/// </summary>
public sealed class EstimateShareLinkService
{
    private const string _purpose = "CrestApps.OrchardCore.Invoicing.EstimateLink";

    private readonly SignedTokenService _tokens;
    private readonly SiteUrlGenerator _urls;
    private readonly ISiteService _siteService;

    public EstimateShareLinkService(SignedTokenService tokens, SiteUrlGenerator urls, ISiteService siteService)
    {
        _tokens = tokens;
        _urls = urls;
        _siteService = siteService;
    }

    /// <summary>
    /// Creates the absolute address of the estimate's public page.
    /// </summary>
    public async Task<string> CreateUrlAsync(Estimate estimate)
        => await _urls.GetUrlAsync(InvoicingConstants.RouteNames.EstimateView, new { token = await CreateTokenAsync(estimate) });

    /// <summary>
    /// Creates the token of a link to the estimate's public page.
    /// </summary>
    public async Task<string> CreateTokenAsync(Estimate estimate)
    {
        ArgumentNullException.ThrowIfNull(estimate);

        var settings = (await _siteService.GetSiteSettingsAsync()).GetOrCreate<InvoicingSettings>();
        var days = Math.Max(settings?.ShareLinkDays ?? 60, (settings?.EstimateValidDays ?? 30) + 30);

        return _tokens.Protect(_purpose, estimate.ItemId, TimeSpan.FromDays(Math.Clamp(days, 1, 3650)));
    }

    /// <summary>
    /// Reads the estimate a token names, or <see langword="null"/> when the token is not valid or has expired.
    /// </summary>
    public string GetEstimateId(string token)
        => string.IsNullOrWhiteSpace(token) ? null : _tokens.Unprotect(_purpose, token);
}
