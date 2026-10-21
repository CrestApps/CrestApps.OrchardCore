using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Marketing.Models;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;
using OrchardCore.Liquid;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Marketing.Services;

/// <summary>
/// Sends campaigns: each one to its segment's customers who accept marketing and have an email, in batches, picking up
/// where the last batch stopped, with the customer's own unsubscribe link.
/// </summary>
public sealed partial class CampaignService
{
    public const int BatchSize = 50;

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
    private readonly SignedTokenService _signedTokenService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public CampaignService(
        ICampaignStore campaigns,
        ISegmentStore segments,
        ICustomerPurchasesStore purchases,
        ICustomerStore customers,
        ILiquidTemplateManager liquid,
        ISiteService siteService,
        SignedTokenService signedTokenService,
        IClock clock,
        ILogger<CampaignService> logger,
        IStringLocalizer<CampaignService> stringLocalizer,
        IEmailService emailService = null)
    {
        _campaigns = campaigns;
        _segments = segments;
        _purchases = purchases;
        _customers = customers;
        _liquid = liquid;
        _siteService = siteService;
        _emailService = emailService;
        _signedTokenService = signedTokenService;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    internal readonly IStringLocalizer S;

    public bool CanSend
        => _emailService is not null;

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
            ["Customer"] = new ObjectValue(new CampaignCustomer(customer, purchases)),
            ["UnsubscribeUrl"] = new StringValue(unsubscribeUrl ?? string.Empty),
            ["StoreName"] = new StringValue(site.SiteName ?? string.Empty),
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
        if (_emailService is null || string.IsNullOrWhiteSpace(to))
        {
            return false;
        }

        var sample = await FindSampleAsync(campaign, cancellationToken);
        var (subject, html) = await RenderAsync(campaign, sample.Customer, sample.Purchases, unsubscribeUrl(sample.Customer is null ? "test" : CreateUnsubscribeToken(sample.Customer.ItemId)));

        return (await _emailService.SendAsync(new MailMessage { To = to.Trim(), Subject = "[Test] " + subject, HtmlBody = html, TextBody = ToText(html) }, cancellationToken: cancellationToken)).Succeeded;
    }

    /// <summary>
    /// Sends the next batch of a sending campaign, or starts a scheduled one whose time has come. Returns whether the
    /// campaign has more to send.
    /// </summary>
    public async Task<bool> SendBatchAsync(Campaign campaign, Func<string, string> unsubscribeUrl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        var now = _clock.UtcNow;

        if (campaign.Status == CampaignStatus.Scheduled && campaign.ScheduledUtc <= now)
        {
            campaign.Status = CampaignStatus.Sending;
            campaign.StartedUtc = now;
        }

        if (campaign.Status != CampaignStatus.Sending || _emailService is null)
        {
            return false;
        }

        var segment = await _segments.FindByIdAsync(campaign.SegmentId, cancellationToken);
        var batch = segment is null ? [] : await _purchases.ListAsync(segment.Rules, now, campaign.Cursor, BatchSize, cancellationToken);

        foreach (var purchases in batch)
        {
            campaign.Cursor = purchases.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

            var customer = await _customers.FindByIdAsync(purchases.ItemId, cancellationToken);

            // Only customers who said yes to marketing get it, and only once.
            if (customer is not { AcceptsMarketing: true, Status: CustomerStatus.Active } || string.IsNullOrWhiteSpace(customer.Email))
            {
                campaign.Skipped++;

                continue;
            }

            var (subject, html) = await RenderAsync(campaign, customer, purchases, unsubscribeUrl(CreateUnsubscribeToken(customer.ItemId)));
            var result = await _emailService.SendAsync(new MailMessage { To = customer.Email, Subject = subject, HtmlBody = html, TextBody = ToText(html) }, cancellationToken: cancellationToken);

            if (result.Succeeded)
            {
                campaign.Sent++;
            }
            else
            {
                campaign.Failed++;

                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Campaign '{CampaignId}' couldn't be emailed to customer '{CustomerId}'.", campaign.ItemId, customer.ItemId);
                }
            }
        }

        if (batch.Count < BatchSize)
        {
            campaign.Status = CampaignStatus.Sent;
            campaign.CompletedUtc = now;
        }

        campaign.ModifiedUtc = now;
        await _campaigns.UpdateAsync(campaign, cancellationToken);

        return campaign.Status == CampaignStatus.Sending;
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

    internal static string ToText(string html)
        => System.Net.WebUtility.HtmlDecode(Tags().Replace(LineBreaks().Replace(html ?? string.Empty, "\n"), string.Empty)).Trim();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex("<br\\s*/?>|</p>|</div>|</h[1-6]>|</li>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreaks();
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
