using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Commerce.Controllers;

/// <summary>
/// The storefront account's home page: a tile for each page the modules add to the account menu.
/// </summary>
[Authorize]
public sealed class AccountController : Controller
{
    // The home page's own menu item, left out of its tiles.
    internal const string HomeItemId = "account-home";

    private readonly INavigationManager _navigationManager;

    public AccountController(INavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
    }

    [HttpGet("account", Name = CommerceConstants.Account.HomeRouteName)]
    public async Task<IActionResult> Index()
    {
        var items = await _navigationManager.BuildMenuAsync(CommerceConstants.Account.MenuName, ControllerContext);

        return View(items.Where(item => !string.IsNullOrEmpty(item.Href) && item.Id != HomeItemId).ToList());
    }
}
