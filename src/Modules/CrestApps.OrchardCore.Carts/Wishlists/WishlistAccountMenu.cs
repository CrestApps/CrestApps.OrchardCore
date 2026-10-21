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
/// Adds Wishlist to the storefront account menu.
/// </summary>
public sealed class WishlistAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public WishlistAccountMenu(IStringLocalizer<WishlistAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Wishlist"], "12", wishlist => wishlist
            .Id("account-wishlist")
            .AddClass("fa-regular fa-heart")
            .Action("Index", "Wishlist", new { area = CartConstants.Features.Carts }));

        return ValueTask.CompletedTask;
    }
}
