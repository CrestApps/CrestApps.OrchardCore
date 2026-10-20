using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Fundraising.Indexes;
using CrestApps.OrchardCore.Fundraising.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Email;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Fundraising.Services;

public interface IDonationStore : ICatalog<Donation>
{
    Task<IReadOnlyList<Donation>> ListByCampaignAsync(string campaignId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Donation>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Donation>> ListByDonorAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class DonationStore : DocumentCatalog<Donation, DonationIndex>, IDonationStore
{
    public DonationStore(ISession session)
        : base(session)
    {
        CollectionName = FundraisingConstants.CollectionName;
    }

    public async Task<IReadOnlyList<Donation>> ListByCampaignAsync(string campaignId, CancellationToken cancellationToken = default)
        => [.. await Session.Query<Donation, DonationIndex>(index => index.CampaignId == campaignId, collection: CollectionName).ListAsync(cancellationToken)];

    public async Task<IReadOnlyList<Donation>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default)
        => [.. await Session.Query<Donation, DonationIndex>(index => index.OrderId == orderId, collection: CollectionName).ListAsync(cancellationToken)];

    public async Task<IReadOnlyList<Donation>> ListByDonorAsync(string userId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(userId)
            ? []
            : [.. await Session.Query<Donation, DonationIndex>(index => index.DonorUserId == userId, collection: CollectionName).OrderByDescending(index => index.CreatedUtc).ListAsync(cancellationToken)];
}

/// <summary>
/// A campaign's progress: what's been given toward its goal, by how many donors, and the days left.
/// </summary>
public sealed record CampaignProgress(decimal Raised, decimal Goal, int Donors, string Currency, int? DaysLeft)
{
    /// <summary>
    /// Gets how far toward the goal it is, from 0 to 100.
    /// </summary>
    public int Percent
        => Goal <= 0m ? 0 : (int)Math.Clamp(Math.Floor(Raised / Goal * 100m), 0m, 100m);
}

/// <summary>
/// Donations: recorded when their order is paid, with a receipt, taken back by a refund, and added up toward each
/// campaign's goal.
/// </summary>
public sealed class DonationService
{
    private readonly IDonationStore _donations;
    private readonly IContentManager _contentManager;
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _serviceProvider;
    private readonly HtmlEncoder _encoder;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public DonationService(
        IDonationStore donations,
        IContentManager contentManager,
        ISiteService siteService,
        IServiceProvider serviceProvider,
        HtmlEncoder encoder,
        IClock clock,
        ILogger<DonationService> logger,
        IStringLocalizer<DonationService> stringLocalizer)
    {
        _donations = donations;
        _contentManager = contentManager;
        _siteService = siteService;
        _serviceProvider = serviceProvider;
        _encoder = encoder;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Records a paid order's donations, each with its receipt number, and emails the receipts. Recording an order twice
    /// records nothing more.
    /// </summary>
    public async Task<IReadOnlyList<Donation>> RecordAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        if ((await _donations.ListByOrderAsync(order.ItemId, cancellationToken)).Count > 0)
        {
            return [];
        }

        var now = _clock.UtcNow;
        var recorded = new List<Donation>();

        foreach (var line in order.Lines ?? [])
        {
            var campaign = await _contentManager.GetAsync(line.ContentItemId, VersionOptions.Latest);

            if (campaign?.TryGet<FundraisingCampaignPart>(out var part) != true)
            {
                continue;
            }

            var campaignCurrency = campaign.TryGet<ProductPart>(out var product) && !string.IsNullOrEmpty(product.Currency) ? product.Currency : order.Currency;
            var amount = line.LineTotal;
            var campaignAmount = await ConvertAsync(amount, order.Currency, campaignCurrency, cancellationToken);

            var donation = new Donation
            {
                ItemId = global::OrchardCore.IdGenerator.GenerateId(),
                CampaignId = campaign.ContentItemId,
                CampaignTitle = campaign.DisplayText,
                OrderId = order.ItemId,
                OrderNumber = order.OrderNumber,
                LineId = line.LineId,
                DonorUserId = order.UserId,
                DonorName = order.CustomerName,
                DonorEmail = order.CustomerEmail,
                Amount = amount,
                Currency = order.Currency,
                CampaignAmount = campaignAmount,
                Tribute = line.Details?.GetValueOrDefault(FundraisingConstants.TributeDetail),
                TaxDeductible = part.TaxDeductible,

                // One per order line, and the same on every receipt printed of it.
                ReceiptNumber = $"{order.OrderNumber}-{order.Lines.IndexOf(line) + 1}",
                CreatedUtc = now,
            };

            await _donations.CreateAsync(donation, cancellationToken);
            recorded.Add(donation);
        }

        var settings = await _siteService.GetSettingsAsync<FundraisingSettings>();

        if (settings.EmailReceipts)
        {
            foreach (var donation in recorded.Where(donation => donation.TaxDeductible))
            {
                await SendReceiptAsync(donation, settings, cancellationToken);
            }
        }

        return recorded;
    }

    /// <summary>
    /// Takes a refund off the order's donations, in proportion to what each was.
    /// </summary>
    public async Task RefundAsync(Order order, OrderRefund refund, CancellationToken cancellationToken = default)
    {
        var donations = await _donations.ListByOrderAsync(order.ItemId, cancellationToken);
        var paid = order.Total ?? order.Subtotal;

        foreach (var donation in donations)
        {
            var share = paid <= 0m ? 0m : Math.Round(donation.Amount * Math.Min(1m, refund.Amount / paid), 2, MidpointRounding.AwayFromZero);

            donation.Refunded = Math.Min(donation.Amount, donation.Refunded + share);
            await _donations.UpdateAsync(donation, cancellationToken);
        }
    }

    /// <summary>
    /// Returns a campaign's progress toward its goal.
    /// </summary>
    public async Task<CampaignProgress> GetProgressAsync(ContentItem campaign, CancellationToken cancellationToken = default)
    {
        campaign.TryGet<FundraisingCampaignPart>(out var part);

        var donations = await _donations.ListByCampaignAsync(campaign.ContentItemId, cancellationToken);
        var raised = donations.Sum(donation => donation.Amount <= 0m ? 0m : donation.CampaignAmount * (donation.Amount - donation.Refunded) / donation.Amount);
        var donors = donations.Where(donation => donation.Refunded < donation.Amount).Select(donation => donation.DonorUserId ?? donation.DonorEmail ?? donation.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var currency = campaign.TryGet<ProductPart>(out var product) ? product.Currency : null;
        int? daysLeft = part?.EndsUtc is { } ends ? Math.Max(0, (int)Math.Ceiling((ends - _clock.UtcNow).TotalDays)) : null;

        return new CampaignProgress(Math.Round(raised, 2, MidpointRounding.AwayFromZero), part?.Goal ?? 0m, donors, currency, daysLeft);
    }

    private async Task<decimal> ConvertAsync(decimal amount, string from, string to, CancellationToken cancellationToken)
    {
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase) || _serviceProvider.GetService<IShopperCurrency>() is not { } currencies)
        {
            return amount;
        }

        return await currencies.ConvertAsync(amount, from, to, isPrice: false, cancellationToken) ?? amount;
    }

    private async Task SendReceiptAsync(Donation donation, FundraisingSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(donation.DonorEmail) || _serviceProvider.GetService<IEmailService>() is not { } email)
        {
            return;
        }

        var site = await _siteService.GetSiteSettingsAsync();
        var organization = string.IsNullOrWhiteSpace(settings.OrganizationName) ? site.SiteName : settings.OrganizationName;
        var amount = ProductPriceFormatter.Format(donation.Amount, donation.Currency);
        var lines = new List<string>
        {
            S["Receipt {0}", donation.ReceiptNumber],
            S["Received from {0} on {1}", donation.DonorName ?? donation.DonorEmail, donation.CreatedUtc.ToString("D", CultureInfo.CurrentCulture)],
            S["Donation to {0}: {1}", donation.CampaignTitle, amount],
        };

        if (!string.IsNullOrWhiteSpace(donation.Tribute))
        {
            lines.Add(S["In honor of {0}", donation.Tribute]);
        }

        if (!string.IsNullOrWhiteSpace(settings.TaxId))
        {
            lines.Add(S["{0}, tax ID {1}", organization, settings.TaxId]);
        }

        if (!string.IsNullOrWhiteSpace(settings.ReceiptStatement))
        {
            lines.Add(settings.ReceiptStatement);
        }

        var text = new StringBuilder().AppendLine(S["Thank you for your donation to {0}.", organization]).AppendLine();
        var html = new StringBuilder().Append("<p>").Append(_encoder.Encode(S["Thank you for your donation to {0}.", organization])).Append("</p><table style=\"border-collapse:collapse\">");

        foreach (var line in lines)
        {
            text.AppendLine(line);
            html.Append("<tr><td style=\"padding:2px 0\">").Append(_encoder.Encode(line)).Append("</td></tr>");
        }

        html.Append("</table><p>").Append(_encoder.Encode(S["Keep this receipt for your records. You'll also find it under Donations in your account."])).Append("</p>");
        text.AppendLine().AppendLine(S["Keep this receipt for your records. You'll also find it under Donations in your account."]);

        var result = await email.SendAsync(new MailMessage
        {
            To = donation.DonorEmail,
            Subject = S["Your donation receipt {0}: {1}", donation.ReceiptNumber, amount],
            TextBody = text.ToString(),
            HtmlBody = html.ToString(),
        }, cancellationToken: cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning("The receipt of donation '{DonationId}' couldn't be sent.", donation.ItemId);
        }
    }
}
