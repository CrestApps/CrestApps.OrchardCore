using CrestApps.OrchardCore.Users.Navigation;
using CrestApps.OrchardCore.Users.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Users.Controllers;

/// <summary>
/// The site's "my account" home page: a tile for each page the modules add to the account menu. The tiles are the
/// <c>AccountOverview</c> shape, so a theme can override them.
/// </summary>
[Authorize]
[Feature(UsersConstants.Feature.Account)]
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

    [HttpGet("account", Name = UsersConstants.Account.HomeRouteName)]
    public async Task<IActionResult> Index()
    {
        var items = await _navigationManager.BuildMenuAsync(UsersConstants.Account.MenuName, ControllerContext);

        return View(await _shapeFactory.CreateAsync<AccountOverviewViewModel>("AccountOverview", model =>
        {
            model.Items = AccountMenuItems.Arrange(items.Where(item => !string.IsNullOrEmpty(item.Href) && item.Id != HomeItemId));
        }));
    }
}
