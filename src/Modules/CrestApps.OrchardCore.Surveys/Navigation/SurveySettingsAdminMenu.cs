using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Surveys.Navigation;

/// <summary>
/// Adds Configuration → Settings → Surveys.
/// </summary>
public sealed class SurveySettingsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveySettingsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveySettingsAdminMenu(IStringLocalizer<SurveySettingsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Configuration"], configuration => configuration
            .Add(S["Settings"], settings => settings
                .Add(S["Surveys"], S["Surveys"].PrefixPosition(), surveys => surveys
                    .Id("surveysSettings")
                    .Action("Index", "Admin", new RouteValueDictionary { { "area", "OrchardCore.Settings" }, { "groupId", SurveysConstants.SettingsGroupId } })
                    .Permission(SurveysPermissions.ManageSurveySettings)
                    .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
