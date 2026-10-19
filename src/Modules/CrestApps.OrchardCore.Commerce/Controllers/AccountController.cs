using CrestApps.OrchardCore.Commerce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Commerce.Controllers;

/// <summary>
/// The storefront account's home page: a tile for each page the modules add to the account menu. The tiles are the
/// <c>AccountOverview</c> shape, so a theme can override them.
/// </summary>
[Authorize]
public sealed class AccountController : Controller
{
    // The home page's own menu item, left out of its tiles.
    internal const string HomeItemId = "account-home";

    private readonly INavigationManager _navigationManager;
    private readonly IShapeFactory _shapeFactory;

    public AccountController(INavigationManager navigationManager, IShapeFactory shapeFactory)
    {
        _navigationManager = navigationManager;
        _shapeFactory = shapeFactory;
    }

    [HttpGet("account", Name = CommerceConstants.Account.HomeRouteName)]
    public async Task<IActionResult> Index()
    {
        var items = await _navigationManager.BuildMenuAsync(CommerceConstants.Account.MenuName, ControllerContext);

        return View(await _shapeFactory.CreateAsync<AccountOverviewViewModel>("AccountOverview", model =>
        {
            model.Items = items.Where(item => !string.IsNullOrEmpty(item.Href) && item.Id != HomeItemId).ToList();
        }));
    }
}
