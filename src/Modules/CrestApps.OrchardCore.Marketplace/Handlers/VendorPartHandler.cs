using CrestApps.OrchardCore.Marketplace.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.Marketplace.Handlers;

/// <summary>
/// Refuses a change of the vendor a product is sold for by a signed-in user who doesn't manage the marketplace, whatever
/// wrote it: the editor, the content API or an import. The vendor is who a sale's earnings are paid to, so only someone
/// who manages the marketplace sets it. The editor already leaves it alone for anyone else; this holds the other paths to
/// the same rule.
/// </summary>
/// <remarks>
/// Work with no signed-in user (setup recipes, background tasks) is not checked: it is the site's own.
/// </remarks>
internal sealed class VendorPartHandler : ContentPartHandler<VendorPart>
{
    private readonly Dictionary<string, string> _loadedVendors = new(StringComparer.Ordinal);
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public VendorPartHandler(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<VendorPartHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    // The vendor as stored, before anything in this request changed it. The first load of an item wins.
    public override Task LoadedAsync(LoadContentContext context, VendorPart part)
    {
        if (!string.IsNullOrEmpty(context.ContentItem?.ContentItemId))
        {
            _loadedVendors.TryAdd(context.ContentItem.ContentItemId, part.VendorId);
        }

        return Task.CompletedTask;
    }

    public override async Task ValidatingAsync(ValidateContentContext context, VendorPart part)
    {
        var stored = _loadedVendors.GetValueOrDefault(context.ContentItem.ContentItemId ?? string.Empty);

        if (string.Equals(NullIfEmpty(part.VendorId), NullIfEmpty(stored), StringComparison.Ordinal) ||
            _httpContextAccessor.HttpContext?.User is not { Identity.IsAuthenticated: true } user ||
            await _authorizationService.AuthorizeAsync(user, MarketplacePermissions.ManageMarketplace))
        {
            return;
        }

        context.Fail(S["Only someone who manages the marketplace can change the vendor a product is sold for."], nameof(VendorPart.VendorId));
    }

    private static string NullIfEmpty(string value)
        => string.IsNullOrEmpty(value) ? null : value;
}
