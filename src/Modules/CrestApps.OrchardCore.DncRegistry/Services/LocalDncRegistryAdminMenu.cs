using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.DncRegistry.Services;

/// <summary>
/// Adds the Local DNC Registry management entry to the admin navigation menu.
/// </summary>
internal sealed class LocalDncRegistryAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalDncRegistryAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LocalDncRegistryAdminMenu(IStringLocalizer<LocalDncRegistryAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Interaction Center"], "80", interactionCenter => interactionCenter
                .AddClass("interaction-center")
                .Id("interactionCenter")
                .Add(S["Local DNC Registry"], S["Local DNC Registry"].PrefixPosition(), local => local
                    .AddClass("local-dnc-registry")
                    .Id("localDncRegistry")
                    .Action("Index", "LocalDncRegistryAdmin", new { area = DncRegistryConstants.Features.Area })
                    .Permission(DncRegistryPermissions.ManageDncRegistrySettings)
                    .LocalNav()
                ),
                priority: 1);

        return ValueTask.CompletedTask;
    }
}
