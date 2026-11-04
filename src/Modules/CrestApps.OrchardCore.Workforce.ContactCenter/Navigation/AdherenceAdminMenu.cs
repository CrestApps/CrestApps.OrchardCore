using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Navigation;

/// <summary>
/// Adds the adherence pages to the Workforce menu.
/// </summary>
public sealed class AdherenceAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdherenceAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdherenceAdminMenu(IStringLocalizer<AdherenceAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Workforce"], S["Workforce"].PrefixPosition("6"), workforce => workforce
            .Add(S["Adherence"], S["Adherence"].PrefixPosition("55"), adherence => adherence
                .Id("workforceAdherence")
                .Add(S["Real-time"], S["Real-time"].PrefixPosition("10"), board => board
                    .Id("workforceAdherenceBoard")
                    .Action("Index", "Adherence", WorkforceContactCenterConstants.Features.Adherence)
                    .Permission(WorkforcePermissions.ViewAdherence)
                    .LocalNav())
                .Add(S["Exceptions"], S["Exceptions"].PrefixPosition("20"), exceptions => exceptions
                    .Id("workforceAdherenceExceptions")
                    .Action("Index", "AdherenceExceptions", WorkforceContactCenterConstants.Features.Adherence)
                    .Permission(WorkforcePermissions.ViewAdherence)
                    .LocalNav())
                .Add(S["Rule Sets"], S["Rule Sets"].PrefixPosition("30"), rules => rules
                    .Id("workforceAdherenceRuleSets")
                    .Action("Index", "AdherenceRuleSets", WorkforceContactCenterConstants.Features.Adherence)
                    .Permission(WorkforcePermissions.ManageAdherence)
                    .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
