using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.DataSources.Services;

/// <summary>
/// Represents the AI data provider admin menu.
/// </summary>
public sealed class AIDataProviderAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataProviderAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIDataProviderAdminMenu(IStringLocalizer<AIDataProviderAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["Data Sources"], S["Data Sources"].PrefixPosition(), dataSources => dataSources
                    .AddClass("ai-data-sources")
                    .Id("aiSources")
                    .Action("Index", "DataSources", AIConstants.Feature.DataSources)
                    .Permission(AIPermissions.ManageAIDataSources)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
