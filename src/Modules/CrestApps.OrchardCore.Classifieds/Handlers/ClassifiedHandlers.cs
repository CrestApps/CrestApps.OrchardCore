using System.Security.Claims;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Classifieds.Models;
using CrestApps.OrchardCore.Classifieds.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Classifieds.Handlers;

/// <summary>
/// An upgrade on a cart line is bought for one of the buyer's own listings, named in its details, and reads as such.
/// </summary>
internal sealed class ClassifiedUpgradeLineHandler : ICartLineHandler
{
    private readonly IContentManager _contentManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public ClassifiedUpgradeLineHandler(IContentManager contentManager, IHttpContextAccessor httpContextAccessor, IStringLocalizer<ClassifiedUpgradeLineHandler> stringLocalizer)
    {
        _contentManager = contentManager;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public async Task PrepareAsync(CartLinePrepareContext context, CancellationToken cancellationToken = default)
    {
        if (context.ContentItem?.Has<ClassifiedUpgradePart>() != true)
        {
            return;
        }

        context.PostedDetails.TryGetValue(ClassifiedsConstants.ListingDetail, out var listingId);

        if (await GetOwnListingAsync(listingId) is null)
        {
            context.Refuse(S["Choose which of your listings {0} is for, from My listings.", context.Title]);

            return;
        }

        context.Details[ClassifiedsConstants.ListingDetail] = listingId;
    }

    public async Task EvaluateAsync(CartLineEvaluateContext context, CancellationToken cancellationToken = default)
    {
        if (context.ContentItem?.Has<ClassifiedUpgradePart>() != true)
        {
            return;
        }

        if (await GetOwnListingAsync(context.GetDetail(ClassifiedsConstants.ListingDetail)) is not { } listing)
        {
            context.Problem ??= S["The listing this is for isn't yours any more."];

            return;
        }

        context.Details.Add(new CartLineDetail(ClassifiedsConstants.ListingDetail, S["For"], listing.DisplayText));
    }

    private async Task<ContentItem> GetOwnListingAsync(string listingId)
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(listingId) || string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var listing = await _contentManager.GetAsync(listingId, VersionOptions.Latest);

        return listing is not null && listing.Has<ClassifiedPart>() && string.Equals(listing.Owner, userId, StringComparison.Ordinal) ? listing : null;
    }
}

/// <summary>
/// Features or bumps a listing when the upgrade bought for it is paid.
/// </summary>
internal sealed class ClassifiedUpgradeOrderHandler : OrderEventHandlerBase
{
    private readonly IContentManager _contentManager;
    private readonly ClassifiedService _classifieds;

    public ClassifiedUpgradeOrderHandler(
        IContentManager contentManager,
        ClassifiedService classifieds)
    {
        _contentManager = contentManager;
        _classifieds = classifieds;
    }

    public override async Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
    {
        foreach (var line in context.Order.Lines ?? [])
        {
            if (line.Details?.GetValueOrDefault(ClassifiedsConstants.ListingDetail) is { Length: > 0 } listingId &&
                (await _contentManager.GetAsync(line.ContentItemId, VersionOptions.Latest))?.TryGet<ClassifiedUpgradePart>(out var upgrade) == true)
            {
                await _classifieds.UpgradeAsync(listingId, upgrade, line.Quantity);
            }
        }
    }
}
