using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Marketing.Outreach;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Core;
using CrestApps.OrchardCore.Outreach.Models;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Localization;
using OrchardCore.Email;
using OrchardCore.Liquid;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Marketing.Services;

/// <summary>
/// Sends campaigns through the outreach engine: each one to its segment's customers who accept marketing and have an
/// email, in batches, picking up where the last batch stopped, with the customer's own unsubscribe link. Marketing
/// depends on Orchard's e-mail feature, so the e-mail service is always there; a site whose e-mail isn't set up can't
/// send a campaign, and is told so.
/// </summary>
public sealed class CampaignService
{
    private const string UnsubscribePurpose = "CrestApps.Marketing.Unsubscribe";

    // An unsubscribe link keeps working for this long.
    private static readonly TimeSpan _unsubscribeLifetime = TimeSpan.FromDays(400);

    private readonly ICampaignStore _campaigns;
    private readonly ISegmentStore _segments;
    private readonly ICustomerPurchasesStore _purchases;
    private readonly ICustomerStore _customers;
    private readonly ILiquidTemplateManager _liquid;
    private readonly ISiteService _siteService;
    private readonly IEmailService _emailService;
    private readonly IEmailProviderResolver _emailProviderResolver;
    private readonly IOutreachEngine _outreachEngine;
    private readonly SignedTokenService _signedTokenService;
    private readonly IClock _clock;

    public CampaignService(
        ICampaignStore campaigns,
        ISegmentStore segments,
        ICustomerPurchasesStore purchases,
        ICustomerStore customers,
        ILiquidTemplateManager liquid,
        ISiteService siteService,
        SignedTokenService signedTokenService,
        IClock clock,
        IStringLocalizer<CampaignService> stringLocalizer,
        IEmailService emailService,
        IEmailProviderResolver emailProviderResolver,
        IOutreachEngine outreachEngine)
    {
        _campaigns = campaigns;
        _segments = segments;
        _purchases = purchases;
        _customers = customers;
        _liquid = liquid;
        _siteService = siteService;
        _emailService = emailService;
        _emailProviderResolver = emailProviderResolver;
        _outreachEngine = outreachEngine;
        _signedTokenService = signedTokenService;
        _clock = clock;
        S = stringLocalizer;
    }

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Gets whether the site's e-mail is set up, so a campaign can be sent.
    /// </summary>
    public async Task<bool> CanSendAsync()
        => await _emailProviderResolver.GetAsync() is not null;

    /// <summary>
    /// Returns the token of a customer's unsubscribe link.
    /// </summary>
    public string CreateUnsubscribeToken(string customerId)
        => _signedTokenService.Protect(UnsubscribePurpose, customerId, _unsubscribeLifetime);

    /// <summary>
    /// Returns the customer an unsubscribe token is for, or <see langword="null"/> when it isn't valid any more.
    /// </summary>
    public string ReadUnsubscribeToken(string token)
        => _signedTokenService.Unprotect(UnsubscribePurpose, token);

    /// <summary>
    /// Returns a campaign's subject and HTML body for a customer.
    /// </summary>
    public async Task<(string Subject, string Html)> RenderAsync(Campaign campaign, Customer customer, CustomerPurchases purchases, string unsubscribeUrl)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        var site = await _siteService.GetSiteSettingsAsync();
        var values = new Dictionary<string, FluidValue>
        {
            ["Customer"] = ObjectValue.Create(new CampaignCustomer(customer, purchases)),
            ["UnsubscribeUrl"] = StringValue.Create(unsubscribeUrl ?? string.Empty),
            ["StoreName"] = StringValue.Create(site.SiteName ?? string.Empty),
        };

        var subject = await _liquid.RenderStringAsync(campaign.Subject ?? string.Empty, NullEncoder.Default, values);
        var html = await _liquid.RenderStringAsync(campaign.Body ?? string.Empty, HtmlEncoder.Default, values);

        // Every campaign carries its unsubscribe link, whether or not the body does.
        if (!string.IsNullOrEmpty(unsubscribeUrl) && !html.Contains(unsubscribeUrl, StringComparison.Ordinal))
        {
            html += $"<p style=\"font-size:12px;color:#6c757d\"><a href=\"{HtmlEncoder.Default.Encode(unsubscribeUrl)}\">{HtmlEncoder.Default.Encode(S["Unsubscribe from these emails"])}</a></p>";
        }

        return (subject.Trim(), html);
    }

    /// <summary>
    /// Sends a campaign to one address, as the first customer of its segment would get it. Returns whether it went.
    /// </summary>
    public async Task<bool> SendTestAsync(Campaign campaign, string to, Func<string, string> unsubscribeUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            return false;
        }

        var sample = await FindSampleAsync(campaign, cancellationToken);
        var (subject, html) = await RenderAsync(campaign, sample.Customer, sample.Purchases, unsubscribeUrl(sample.Customer is null ? "test" : CreateUnsubscribeToken(sample.Customer.ItemId)));

        return (await _emailService.SendAsync(new MailMessage { To = to.Trim(), Subject = "[Test] " + subject, HtmlBody = html, TextBody = OutreachText.FromHtml(html) }, cancellationToken: cancellationToken)).Succeeded;
    }

    /// <summary>
    /// Starts a scheduled campaign whose time has come, and hands a sending campaign to the outreach engine, which sends
    /// it to its segment in batches. A campaign that was half sent before it carries its counts and position over.
    /// Returns whether a run was started.
    /// </summary>
    public async Task<bool> StartSendingAsync(Campaign campaign, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        var now = _clock.UtcNow;

        if (campaign.Status == CampaignStatus.Scheduled && campaign.ScheduledUtc <= now)
        {
            campaign.Status = CampaignStatus.Sending;
            campaign.StartedUtc = now;
        }

        if (campaign.Status != CampaignStatus.Sending || !string.IsNullOrEmpty(campaign.OutreachRunId))
        {
            return false;
        }

        var run = await _outreachEngine.StartAsync(new OutreachRun
        {
            Name = campaign.Name,
            OwnerType = MarketingOutreachConstants.CampaignOwnerType,
            OwnerId = campaign.ItemId,
            AudienceSource = MarketingOutreachConstants.SegmentAudienceSource,
            Sender = OutreachConstants.EmailSender,

            // Before each e-mail the engine asks the consent service whether the customer still accepts marketing, so
            // an unsubscribe recorded after the audience page was read still stops the e-mail.
            ConsentChannel = ConsentChannels.Email,
            ConsentContactKind = ConsentContactKinds.Customer,
            Renderer = MarketingOutreachConstants.CampaignRenderer,
            PageSize = MarketingOutreachConstants.PageSize,
            Template = new OutreachTemplate { Subject = campaign.Subject, Body = campaign.Body, IsHtml = true },
            Cursor = campaign.Cursor,
            Sent = campaign.Sent,
            Skipped = campaign.Skipped,
            Failed = campaign.Failed,
        }, cancellationToken);

        campaign.OutreachRunId = run.ItemId;
        campaign.ModifiedUtc = now;
        await _campaigns.UpdateAsync(campaign, cancellationToken);

        return true;
    }

    /// <summary>
    /// Cancels a scheduled or sending campaign, and stops its outreach run so nothing more goes out.
    /// </summary>
    public async Task<bool> CancelAsync(Campaign campaign, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        if (campaign.Status is not (CampaignStatus.Scheduled or CampaignStatus.Sending))
        {
            return false;
        }

        campaign.Status = CampaignStatus.Canceled;
        campaign.ModifiedUtc = _clock.UtcNow;
        await _campaigns.UpdateAsync(campaign, cancellationToken);
        await _outreachEngine.CancelAsync(campaign.OutreachRunId, cancellationToken);

        return true;
    }

    /// <summary>
    /// Returns how many of a segment's customers a campaign would go to now: those who accept marketing and have an email.
    /// </summary>
    public async Task<(int InSegment, int Reachable)> CountAsync(Segment segment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(segment);

        var now = _clock.UtcNow;
        var total = await _purchases.CountAsync(segment.Rules, now, cancellationToken);
        var reachable = 0;
        string cursor = null;

        // Reading every customer is fine for a store's segment sizes; a preview stops counting at 10,000.
        for (var read = 0; read < 10_000;)
        {
            var batch = await _purchases.ListAsync(segment.Rules, now, cursor, 500, cancellationToken);

            foreach (var purchases in batch)
            {
                if (await _customers.FindByIdAsync(purchases.ItemId, cancellationToken) is { AcceptsMarketing: true, Status: CustomerStatus.Active, Email.Length: > 0 })
                {
                    reachable++;
                }
            }

            read += batch.Count;

            if (batch.Count < 500)
            {
                break;
            }

            cursor = batch[^1].Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return (total, reachable);
    }

    private async Task<(Customer Customer, CustomerPurchases Purchases)> FindSampleAsync(Campaign campaign, CancellationToken cancellationToken)
    {
        if (await _segments.FindByIdAsync(campaign.SegmentId, cancellationToken) is { } segment &&
            await _purchases.ListAsync(segment.Rules, _clock.UtcNow, null, 1, cancellationToken) is [var purchases, ..])
        {
            return (await _customers.FindByIdAsync(purchases.ItemId, cancellationToken), purchases);
        }

        return (null, null);
    }
}

/// <summary>
/// What a campaign's Liquid knows about the customer it's for.
/// </summary>
public sealed class CampaignCustomer
{
    public CampaignCustomer(Customer customer, CustomerPurchases purchases)
    {
        FirstName = customer?.FirstName ?? customer?.GetName() ?? string.Empty;
        LastName = customer?.LastName ?? string.Empty;
        Name = customer?.GetName() ?? string.Empty;
        Email = customer?.Email ?? string.Empty;
        OrderCount = purchases?.OrderCount ?? 0;
        TotalSpent = purchases?.TotalSpent ?? 0m;
        LastOrderUtc = purchases?.LastOrderUtc;
    }

    public string FirstName { get; }

    public string LastName { get; }

    public string Name { get; }

    public string Email { get; }

    public int OrderCount { get; }

    public decimal TotalSpent { get; }

    public DateTime? LastOrderUtc { get; }
}
