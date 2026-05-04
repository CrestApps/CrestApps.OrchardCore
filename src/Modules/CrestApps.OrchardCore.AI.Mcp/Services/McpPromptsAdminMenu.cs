using CrestApps.OrchardCore.AI.Mcp.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

internal sealed class McpPromptsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpPromptsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public McpPromptsAdminMenu(IStringLocalizer<McpPromptsAdminMenu> stringLocalizer)
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
                    .Add(S["Prompts"], S["Prompts"].PrefixPosition(), prompts => prompts
                        .AddClass("ai-mcp-prompts")
                        .Id("aiMcpPrompts")
                        .Action("Index", "Prompts", "CrestApps.OrchardCore.AI.Mcp")
                        .Permission(McpPermissions.ManageMcpPrompts)
                        .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
