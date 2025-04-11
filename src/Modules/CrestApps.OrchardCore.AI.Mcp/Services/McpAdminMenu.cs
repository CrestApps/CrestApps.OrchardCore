using CrestApps.OrchardCore.AI.Mcp.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

internal sealed class McpAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public McpAdminMenu(IStringLocalizer<McpAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["MCP Connections"], S["MCP Connections"].PrefixPosition(), connections => connections
                    .AddClass("ai-mcp-connections")
                    .Id("aiMcpConnections")
                    .Action("Index", "Connections", "CrestApps.OrchardCore.AI.Mcp")
                    .Permission(McpPermissions.ManageMcpConnections)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
