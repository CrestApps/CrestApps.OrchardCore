using System.Globalization;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Fundraising.Models;
using CrestApps.OrchardCore.Fundraising.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Fundraising.Handlers;

/// <summary>
/// A donation on a cart line: the amount the donor chose is its price, it can't be discounted, it's one gift a line,
/// and it reads with who it honors.
/// </summary>
internal sealed class DonationLineHandler : ICartLineHandler
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DonationLineHandler(IClock clock, IStringLocalizer<DonationLineHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    public Task PrepareAsync(CartLinePrepareContext context, CancellationToken cancellationToken = default)
    {
        if (context.ContentItem?.TryGet<FundraisingCampaignPart>(out var part) != true)
        {
            return Task.CompletedTask;
        }

        context.PostedDetails.TryGetValue(FundraisingConstants.AmountDetail, out var posted);

        if (DonationAmounts.Parse(posted) is not { } amount || amount < Math.Max(0.01m, part.MinimumAmount))
        {
            context.Refuse(S["Choose how much to give to {0}: at least {1}.", context.Title, Math.Max(0.01m, part.MinimumAmount).ToString("0.##", CultureInfo.CurrentCulture)]);

            return Task.CompletedTask;
        }

        context.Details[FundraisingConstants.AmountDetail] = amount.ToString(CultureInfo.InvariantCulture);

        if (context.PostedDetails.TryGetValue(FundraisingConstants.TributeDetail, out var tribute) && !string.IsNullOrWhiteSpace(tribute))
        {
            var trimmed = tribute.Trim();

            context.Details[FundraisingConstants.TributeDetail] = trimmed.Length > 120 ? trimmed[..120] : trimmed;
        }

        return Task.CompletedTask;
    }

    public Task EvaluateAsync(CartLineEvaluateContext context, CancellationToken cancellationToken = default)
    {
        if (context.ContentItem?.TryGet<FundraisingCampaignPart>(out var part) != true)
        {
            return Task.CompletedTask;
        }

        if (part.EndsUtc < _clock.UtcNow)
        {
            context.Problem ??= S["{0} has ended and doesn't take donations any more.", context.Title];
        }

        // What the donor chose, in the currency they chose it in, as is.
        if (DonationAmounts.Parse(context.GetDetail(FundraisingConstants.AmountDetail)) is { } amount)
        {
            context.UnitPrice = amount;
        }

        context.MaxQuantity = Math.Min(context.MaxQuantity, 1);
        context.ExcludeFromDiscounts = true;
        context.Details.Add(new CartLineDetail(FundraisingConstants.AmountDetail, S["Donation"], ProductPriceFormatter.Format(context.UnitPrice, context.Price?.Currency)));

        if (context.GetDetail(FundraisingConstants.TributeDetail) is { Length: > 0 } tribute)
        {
            context.Details.Add(new CartLineDetail(FundraisingConstants.TributeDetail, S["In honor of"], tribute));
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Records donations, with their receipts, when their order is paid, and takes refunds off them.
/// </summary>
internal sealed class DonationOrderHandler : OrderEventHandlerBase
{
    private readonly DonationService _donations;

    public DonationOrderHandler(DonationService donations)
    {
        _donations = donations;
    }

    public override Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
        => _donations.RecordAsync(context.Order, cancellationToken);

    public override Task RefundedAsync(OrderRefundedContext context, CancellationToken cancellationToken = default)
        => _donations.RefundAsync(context.Order, context.Refund, cancellationToken);
}

/// <summary>
/// Reads an amount a donor chose. The donate form's buttons and its number field both send it in the invariant culture,
/// whatever the donor's language, so "25,50" is never read as 2550.
/// </summary>
public static class DonationAmounts
{
    public static decimal? Parse(string value)
        => decimal.TryParse(value?.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) && amount > 0m
            ? Math.Round(amount, 2, MidpointRounding.AwayFromZero)
            : null;
}
