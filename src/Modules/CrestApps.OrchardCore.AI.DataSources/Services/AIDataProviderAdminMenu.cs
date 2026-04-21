using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.DataSources.Services;

public sealed class AIDataProviderAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

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
