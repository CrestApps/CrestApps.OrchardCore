using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Classifieds.BackgroundTasks;
using CrestApps.OrchardCore.Classifieds.Controllers;
using CrestApps.OrchardCore.Classifieds.Drivers;
using CrestApps.OrchardCore.Classifieds.Handlers;
using CrestApps.OrchardCore.Classifieds.Indexes;
using CrestApps.OrchardCore.Classifieds.Migrations;
using CrestApps.OrchardCore.Classifieds.Models;
using CrestApps.OrchardCore.Classifieds.Services;
using CrestApps.OrchardCore.Classifieds.ViewModels;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Classifieds;

/// <summary>
/// Adds the poster's listings to the storefront account.
/// </summary>
public sealed class ClassifiedsAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public ClassifiedsAccountMenu(IStringLocalizer<ClassifiedsAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["My listings"], "23", listings => listings
            .Id("account-listings")
            .AddClass("fa-solid fa-bullhorn")
            .Action(nameof(ClassifiedsController.Own), "Classifieds", new { area = ClassifiedsConstants.Feature })
            .Permission(ClassifiedsPermissions.PostClassifieds));

        return ValueTask.CompletedTask;
    }
}
