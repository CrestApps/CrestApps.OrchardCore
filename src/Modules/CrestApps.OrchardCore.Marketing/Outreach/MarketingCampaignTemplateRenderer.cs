using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Marketing.Services;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Models;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Marketing.Outreach;

/// <summary>
/// Renders a campaign's subject and HTML body for one customer, with their own unsubscribe link.
/// </summary>
public sealed class MarketingCampaignTemplateRenderer : IOutreachTemplateRenderer
{
    private readonly ICampaignStore _campaigns;
    private readonly ICustomerStore _customers;
    private readonly ICustomerPurchasesStore _purchases;
    private readonly CampaignService _campaignService;
    private readonly ISiteService _siteService;
    private readonly LinkGenerator _linkGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketingCampaignTemplateRenderer"/> class.
    /// </summary>
    public MarketingCampaignTemplateRenderer(
        ICampaignStore campaigns,
        ICustomerStore customers,
        ICustomerPurchasesStore purchases,
        CampaignService campaignService,
        ISiteService siteService,
        LinkGenerator linkGenerator)
    {
        _campaigns = campaigns;
        _customers = customers;
        _purchases = purchases;
        _campaignService = campaignService;
        _siteService = siteService;
        _linkGenerator = linkGenerator;
    }

    /// <inheritdoc/>
    public string Name
        => MarketingOutreachConstants.CampaignRenderer;

    /// <inheritdoc/>
    public async Task<OutreachContent> RenderAsync(OutreachRun run, OutreachRecipient recipient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(recipient);

        var campaign = await _campaigns.FindByIdAsync(run.OwnerId, cancellationToken)
            ?? throw new InvalidOperationException($"The campaign '{run.OwnerId}' no longer exists.");

        var baseUrl = (await _siteService.GetSiteSettingsAsync()).BaseUrl;

        // Without the site's address there's no unsubscribe link, so nothing goes out: the batch fails and is retried,
        // and the run fails if the address is still missing.
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Campaign emails need the site's base URL for their unsubscribe link.");
        }

        var customer = await _customers.FindByIdAsync(recipient.ContactId, cancellationToken);
        var purchases = await _purchases.FindByIdAsync(recipient.ContactId, cancellationToken);

        // The site's address holds the tenant's prefix, so the route's path goes after it as is.
        var unsubscribeUrl = baseUrl.TrimEnd('/') + _linkGenerator.GetPathByRouteValues(
            MarketingConstants.UnsubscribeRouteName,
            new { token = _campaignService.CreateUnsubscribeToken(recipient.ContactId) });

        var (subject, html) = await _campaignService.RenderAsync(campaign, customer, purchases, unsubscribeUrl);

        return new OutreachContent
        {
            Subject = subject,
            Body = html,
            IsHtml = true,
        };
    }
}
