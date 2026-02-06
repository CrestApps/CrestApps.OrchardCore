using CrestApps.OrchardCore.AI.Mcp.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

internal sealed class McpResourcesAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public McpResourcesAdminMenu(IStringLocalizer<McpResourcesAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["Model Context Protocol"], S["Model Context Protocol"].PrefixPosition(), mcp => mcp
                    .AddClass("ai-mcp")
                    .Id("aiMcp")
                    .Add(S["Resources"], S["Resources"].PrefixPosition(), resources => resources
                        .AddClass("ai-mcp-resources")
                        .Id("aiMcpResources")
                        .Action("Index", "Resources", "CrestApps.OrchardCore.AI.Mcp")
                        .Permission(McpPermissions.ManageMcpResources)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
