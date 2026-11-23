using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Consent.Unsubscribe;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.MailingLists;
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
/// email, in batches, picking up where the last batch stopped, with an unsubscribe link to the site's e-mail
/// preferences page. Marketing depends on Orchard's e-mail feature, so the e-mail service is always there; a site whose
/// e-mail isn't set up can't send a campaign, and is told so.
/// </summary>
public sealed class CampaignService
{
    // The purpose of the links campaigns carried before the e-mail preferences page; only read now, so e-mails already
    // sent keep working until their links expire.
    private const string UnsubscribePurpose = "CrestApps.Marketing.Unsubscribe";

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
    private readonly IUnsubscribeLinks _unsubscribeLinks;
    private readonly IClock _clock;

    public CampaignService(
        ICampaignStore campaigns,
        ISegmentStore segments,
        ICustomerPurchasesStore purchases,
        ICustomerStore customers,
        ILiquidTemplateManager liquid,
        ISiteService siteService,
        SignedTokenService signedTokenService,
        IUnsubscribeLinks unsubscribeLinks,
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
        _unsubscribeLinks = unsubscribeLinks;
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
    /// Returns the unsubscribe link a campaign e-mail to an address carries: the site's e-mail preferences page, for all
    /// marketing e-mail. Returns <see langword="null"/> when the address is empty or the site has no base URL.
    /// </summary>
    /// <param name="email">The address the e-mail goes to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task<string> CreateUnsubscribeUrlAsync(string email, CancellationToken cancellationToken = default)
        => _unsubscribeLinks.CreateUrlAsync(email, UnsubscribeScope.AllMarketing, cancellationToken);

    /// <summary>
    /// Returns the unsubscribe link an e-mail of a campaign to an address carries: the site's e-mail preferences page,
    /// for the campaign's mailing list when it goes to one, and for all marketing e-mail when it goes to a segment.
    /// Returns <see langword="null"/> when the address is empty or the site has no base URL.
    /// </summary>
    /// <param name="campaign">The campaign.</param>
    /// <param name="email">The address the e-mail goes to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task<string> CreateUnsubscribeUrlAsync(Campaign campaign, string email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        return _unsubscribeLinks.CreateUrlAsync(email, GetUnsubscribeScope(campaign), cancellationToken);
    }

    /// <summary>
    /// Returns what the unsubscribe links of a campaign stop: the campaign's mailing list when it goes to one, and all
    /// marketing e-mail when it goes to a segment.
    /// </summary>
    /// <param name="campaign">The campaign.</param>
    public static UnsubscribeScope GetUnsubscribeScope(Campaign campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        return CampaignAudienceKinds.IsMailingList(campaign.AudienceKind)
            ? new UnsubscribeScope { Kind = MailingListsConstants.UnsubscribeScopeKind, Id = campaign.MailingListId }
            : UnsubscribeScope.AllMarketing;
    }

    /// <summary>
    /// Returns the customer the token of an unsubscribe link sent before the e-mail preferences page is for, or
    /// <see langword="null"/> when it isn't valid any more.
    /// </summary>
    public string ReadUnsubscribeToken(string token)
        => _signedTokenService.Unprotect(UnsubscribePurpose, token);

    /// <summary>
    /// Returns a campaign's subject and HTML body for a customer.
    /// </summary>
    public Task<(string Subject, string Html)> RenderAsync(Campaign campaign, Customer customer, CustomerPurchases purchases, string unsubscribeUrl)
        => RenderAsync(campaign, new CampaignCustomer(customer, purchases), new CampaignRecipient(customer?.GetName(), customer?.Email), unsubscribeUrl);

    /// <summary>
    /// Returns a campaign's subject and HTML body for one recipient, with the unsubscribe link added at the end when the
    /// body has none. A member of a mailing list is not a customer: its <c>Customer</c> values carry only the member's
    /// name and address.
    /// </summary>
    /// <param name="campaign">The campaign.</param>
    /// <param name="customer">What the Liquid knows as <c>Customer</c>.</param>
    /// <param name="recipient">What the Liquid knows as <c>Recipient</c>: the name and address the e-mail goes to.</param>
    /// <param name="unsubscribeUrl">The recipient's unsubscribe link.</param>
    public async Task<(string Subject, string Html)> RenderAsync(Campaign campaign, CampaignCustomer customer, CampaignRecipient recipient, string unsubscribeUrl)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        var site = await _siteService.GetSiteSettingsAsync();
        var values = new Dictionary<string, FluidValue>
        {
            ["Customer"] = ObjectValue.Create(customer ?? CampaignCustomer.ForRecipient(null, null)),
            ["Recipient"] = ObjectValue.Create(recipient ?? new CampaignRecipient(null, null)),
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
    /// Sends a campaign to one address, as the first customer of its segment would get it, or, for a campaign to a
    /// mailing list, as a member with that address would. Returns whether it went. The test's unsubscribe link is for the
    /// test address, so following it never unsubscribes the sample customer.
    /// </summary>
    public async Task<bool> SendTestAsync(Campaign campaign, string to, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        if (string.IsNullOrWhiteSpace(to))
        {
            return false;
        }

        var unsubscribeUrl = await CreateUnsubscribeUrlAsync(campaign, to, cancellationToken);
        string subject;
        string html;

        if (CampaignAudienceKinds.IsMailingList(campaign.AudienceKind))
        {
            (subject, html) = await RenderAsync(campaign, CampaignCustomer.ForRecipient(null, to.Trim()), new CampaignRecipient(null, to.Trim()), unsubscribeUrl);
        }
        else
        {
            var sample = await FindSampleAsync(campaign, cancellationToken);

            (subject, html) = await RenderAsync(campaign, sample.Customer, sample.Purchases, unsubscribeUrl);
        }

        return (await _emailService.SendAsync(new MailMessage { To = to.Trim(), Subject = "[Test] " + subject, HtmlBody = html, TextBody = OutreachText.FromHtml(html) }, cancellationToken: cancellationToken)).Succeeded;
    }

    /// <summary>
    /// Starts a scheduled campaign whose time has come, and hands a sending campaign to the outreach engine, which sends
    /// it to its segment, or to its mailing list's subscribed members, in batches. A campaign that was half sent before
    /// it carries its counts and position over. Returns whether a run was started.
    /// </summary>
    /// <remarks>
    /// A run to a mailing list reads the list through the mailing lists Outreach audience; while the Mailing Lists
    /// feature is off, the engine keeps the run waiting rather than failing it.
    /// </remarks>
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

        var run = await _outreachEngine.StartAsync(BuildRun(campaign), cancellationToken);

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

    /// <summary>
    /// Returns the outreach run that sends a campaign: to its segment's customers, or to its mailing list's subscribed
    /// members, by e-mail, checking before each e-mail that the recipient has not stopped marketing. The run carries the
    /// campaign's counts and position over.
    /// </summary>
    /// <param name="campaign">The campaign.</param>
    internal static OutreachRun BuildRun(Campaign campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        var toList = CampaignAudienceKinds.IsMailingList(campaign.AudienceKind);
        var run = new OutreachRun
        {
            Name = campaign.Name,
            OwnerType = MarketingOutreachConstants.CampaignOwnerType,
            OwnerId = campaign.ItemId,
            AudienceSource = toList
                ? MailingListsConstants.OutreachAudienceSource
                : MarketingOutreachConstants.SegmentAudienceSource,
            Sender = OutreachConstants.EmailSender,

            // Before each e-mail the engine asks the consent service whether the recipient still accepts marketing, so
            // an unsubscribe recorded after the audience page was read still stops the e-mail. A member of a list is
            // checked by address; a customer by the customer record too.
            ConsentChannel = ConsentChannels.Email,
            ConsentContactKind = toList ? null : ConsentContactKinds.Customer,
            ConsentPurpose = ConsentPurposes.Marketing,
            Renderer = MarketingOutreachConstants.CampaignRenderer,
            PageSize = MarketingOutreachConstants.PageSize,
            Template = new OutreachTemplate { Subject = campaign.Subject, Body = campaign.Body, IsHtml = true },
            Cursor = campaign.Cursor,
            Sent = campaign.Sent,
            Skipped = campaign.Skipped,
            Failed = campaign.Failed,
        };

        if (toList)
        {
            // The mailing lists audience reads the list from the run, and the run stays owned by the campaign, so its
            // progress is still copied back onto it.
            run.Properties[MailingListsConstants.OutreachListIdProperty] = campaign.MailingListId;
        }

        return run;
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
/// What a campaign's Liquid knows about the customer it's for. For a member of a mailing list, who is not a customer, it
/// holds the member's name and address only.
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

    private CampaignCustomer(string name, string email)
    {
        FirstName = name ?? string.Empty;
        LastName = string.Empty;
        Name = name ?? string.Empty;
        Email = email ?? string.Empty;
    }

    /// <summary>
    /// Returns the <c>Customer</c> values of a recipient who is not a customer, such as a member of a mailing list: the
    /// name (as the first name too, since it isn't split) and the address, with no orders.
    /// </summary>
    /// <param name="name">The recipient's name, or <see langword="null"/>.</param>
    /// <param name="email">The recipient's address.</param>
    public static CampaignCustomer ForRecipient(string name, string email)
        => new(name, email);

    public string FirstName { get; }

    public string LastName { get; }

    public string Name { get; }

    public string Email { get; }

    public int OrderCount { get; }

    public decimal TotalSpent { get; }

    public DateTime? LastOrderUtc { get; }
}

/// <summary>
/// What a campaign's Liquid knows as <c>Recipient</c>: the name and address the e-mail goes to, the same for a customer
/// of a segment and a member of a mailing list.
/// </summary>
public sealed class CampaignRecipient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CampaignRecipient"/> class.
    /// </summary>
    /// <param name="name">The recipient's name, or <see langword="null"/>.</param>
    /// <param name="email">The address the e-mail goes to.</param>
    public CampaignRecipient(string name, string email)
    {
        Name = name ?? string.Empty;
        Email = email ?? string.Empty;
    }

    /// <summary>
    /// Gets the recipient's name, or an empty text when it isn't known.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the address the e-mail goes to.
    /// </summary>
    public string Email { get; }
}
