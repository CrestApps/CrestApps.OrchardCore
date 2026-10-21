using System.Security.Claims;
using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Carts.Wishlists;

/// <summary>
/// Registers wishlists: products signed-in shoppers save for later from the product page, listed in their account.
/// </summary>
[Feature(CartConstants.Features.Wishlists)]
public sealed class WishlistsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<WishlistMigrations>()
            .AddIndexProvider<WishlistIndexProvider>();
        services.AddScoped<IWishlistStore, WishlistStore>();
        services.AddScoped<WishlistService>();
        services.AddContentPart<ProductPart>()
            .UseDisplayDriver<WishlistButtonDisplayDriver>();
        services.AddNavigationProvider<WishlistAccountMenu>();
    }
}

/// <summary>
/// Save to wishlist, on the product's page.
/// </summary>
internal sealed class WishlistButtonDisplayDriver : ContentPartDisplayDriver<ProductPart>
{
    private readonly WishlistService _wishlists;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public WishlistButtonDisplayDriver(WishlistService wishlists, IHttpContextAccessor httpContextAccessor)
    {
        _wishlists = wishlists;
        _httpContextAccessor = httpContextAccessor;
    }

    public override IDisplayResult Display(ProductPart part, BuildPartDisplayContext context)
    {
        if (context.DisplayType != "Detail")
        {
            return null;
        }

        return Initialize<WishlistButtonViewModel>("WishlistButton", async model =>
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var userId = httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            model.ContentItemId = part.ContentItem.ContentItemId;
            model.IsSignedIn = !string.IsNullOrEmpty(userId);
            model.IsSaved = model.IsSignedIn && await _wishlists.ContainsAsync(userId, model.ContentItemId);
            model.ReturnUrl = httpContext is null ? null : httpContext.Request.PathBase + httpContext.Request.Path + httpContext.Request.QueryString;
        }).Location("Detail", "Content:before:6");
    }
}
