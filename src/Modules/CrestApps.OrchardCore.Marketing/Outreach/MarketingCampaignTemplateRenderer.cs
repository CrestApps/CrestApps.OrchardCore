using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Marketing.Services;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Models;

namespace CrestApps.OrchardCore.Marketing.Outreach;

/// <summary>
/// Renders a campaign's subject and HTML body for one customer of its segment, or one member of its mailing list, with
/// an unsubscribe link to the site's e-mail preferences page for the recipient's address: for the mailing list, or for
/// all marketing e-mail.
/// </summary>
public sealed class MarketingCampaignTemplateRenderer : IOutreachTemplateRenderer
{
    private readonly ICampaignStore _campaigns;
    private readonly ICustomerStore _customers;
    private readonly ICustomerPurchasesStore _purchases;
    private readonly CampaignService _campaignService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketingCampaignTemplateRenderer"/> class.
    /// </summary>
    /// <param name="campaigns">The campaign store.</param>
    /// <param name="customers">The customer store.</param>
    /// <param name="purchases">The store of customers' counted purchases.</param>
    /// <param name="campaignService">The campaign service, which renders the campaign and creates its unsubscribe link.</param>
    public MarketingCampaignTemplateRenderer(
        ICampaignStore campaigns,
        ICustomerStore customers,
        ICustomerPurchasesStore purchases,
        CampaignService campaignService)
    {
        _campaigns = campaigns;
        _customers = customers;
        _purchases = purchases;
        _campaignService = campaignService;
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

        // The engine skips a recipient without an address before rendering, so the link is for the address the e-mail
        // goes to: for the campaign's mailing list, or for all marketing e-mail.
        var unsubscribeUrl = await _campaignService.CreateUnsubscribeUrlAsync(campaign, recipient.Address, cancellationToken);

        // Without the site's address there's no unsubscribe link, so nothing goes out: the batch fails and is retried,
        // and the run fails if the address is still missing.
        if (string.IsNullOrEmpty(unsubscribeUrl))
        {
            throw new InvalidOperationException("Campaign emails need the site's base URL for their unsubscribe link.");
        }

        string subject;
        string html;

        if (CampaignAudienceKinds.IsMailingList(campaign.AudienceKind))
        {
            // A member of a mailing list is not a customer: the Liquid gets the member's name and address only.
            (subject, html) = await _campaignService.RenderAsync(
                campaign,
                CampaignCustomer.ForRecipient(recipient.DisplayName, recipient.Address),
                new CampaignRecipient(recipient.DisplayName, recipient.Address),
                unsubscribeUrl);
        }
        else
        {
            var customer = string.IsNullOrEmpty(recipient.ContactId) ? null : await _customers.FindByIdAsync(recipient.ContactId, cancellationToken);
            var purchases = string.IsNullOrEmpty(recipient.ContactId) ? null : await _purchases.FindByIdAsync(recipient.ContactId, cancellationToken);

            (subject, html) = await _campaignService.RenderAsync(campaign, customer, purchases, unsubscribeUrl);
        }

        return new OutreachContent
        {
            Subject = subject,
            Body = html,
            IsHtml = true,
        };
    }
}
