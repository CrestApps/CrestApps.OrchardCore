using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Agreements.Navigation;

/// <summary>
/// Adds Agreements to the Content menu.
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
        // Agreements are records about accounts that several modules use (tickets, field service), so they sit under
        // Content, like Teams, rather than at the top of the menu or under one of the modules that read them.
        builder.Add(S["Content"], content => content
            .Add(S["Agreements"], S["Agreements"].PrefixPosition(), agreements => agreements
                .Id("agreements")
                .Action("Index", "Agreements", AgreementsConstants.Features.Agreements)
                .Permission(AgreementsPermissions.ViewAgreements)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
