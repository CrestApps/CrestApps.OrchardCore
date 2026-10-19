using CrestApps.OrchardCore.Products.Controllers;
using Microsoft.Extensions.Localization;
using OrchardCore.Contents;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Adds Import and export under Commerce in the admin menu.
/// </summary>
internal sealed class ProductTransferAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public ProductTransferAdminMenu(IStringLocalizer<ProductTransferAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                .Add(S["Import and export"], S["Import and export"].PrefixPosition("0.15"), transfer => transfer
                    .AddClass("commerce-product-transfer")
                    .Id("commerceProductTransfer")
                    .Action(nameof(ProductTransferController.Index), "ProductTransfer", new { area = "CrestApps.OrchardCore.Products" })
                    .Permission(CommonPermissions.EditContent)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
