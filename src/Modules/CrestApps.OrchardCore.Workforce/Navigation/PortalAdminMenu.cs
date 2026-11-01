using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Navigation;

/// <summary>
/// Adds a link to the My Work portal to the Workforce menu.
/// </summary>
public sealed class PortalAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PortalAdminMenu(IStringLocalizer<PortalAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Workforce"], workforce => workforce
            .Add(S["My Work Portal"], S["My Work Portal"].PrefixPosition("99"), portal => portal
                .Id("workforceMyWork")
                .Url("~/my-work")
                .Permission(WorkforcePermissions.UseWorkforcePortal)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
