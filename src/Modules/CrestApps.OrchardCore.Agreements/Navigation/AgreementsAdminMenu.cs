using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Agreements.Navigation;

/// <summary>
/// Adds Agreements to the admin menu.
/// </summary>
public sealed class AgreementsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgreementsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AgreementsAdminMenu(IStringLocalizer<AgreementsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Agreements"], S["Agreements"].PrefixPosition("40"), agreements => agreements
            .AddClass("agreements")
            .Id("agreements")
            .Action("Index", "Agreements", AgreementsConstants.Features.Agreements)
            .Permission(AgreementsPermissions.ViewAgreements)
            .LocalNav());

        return ValueTask.CompletedTask;
    }
}
