using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Agent.Autoroute;
using CrestApps.OrchardCore.AI.Agent.Menus;
using CrestApps.OrchardCore.AI.Agent.Seo;
using CrestApps.OrchardCore.AI.Agent.Sitemaps;
using CrestApps.OrchardCore.AI.Agent.Taxonomies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Agent;

/// <summary>
/// The Orchard Core tools of the AI Agent module for addresses, the home page, menus, taxonomies, robots.txt and
/// sitemaps are registered with a title, a description and a category, can be picked on a profile, and ask for approval
/// exactly where the tool coverage plan says so.
/// </summary>
public sealed class OrchardSiteStructureToolRegistrationTests
{
    public static TheoryData<string, bool> Tools => new()
    {
        { FindContentItemByHandleTool.TheName, false },
        { SetHomepageTool.TheName, false },
        { GetTaxonomyTermsTool.TheName, false },
        { SaveTaxonomyTermTool.TheName, false },
        { DeleteTaxonomyTermTool.TheName, true },
        { GetMenuTreeTool.TheName, false },
        { SaveMenuItemTool.TheName, false },
        { DeleteMenuItemTool.TheName, true },
        { GetRobotsTxtTool.TheName, false },
        { ListSitemapsTool.TheName, false },
        { SaveSitemapTool.TheName, false },
        { DeleteSitemapTool.TheName, true },
        { PurgeSitemapCacheTool.TheName, false },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName, bool requiresApproval)
    {
        // Arrange
        var tools = GetTools();

        // Act
        var found = tools.TryGetValue(toolName, out var tool);

        // Assert
        Assert.True(found, $"The tool '{toolName}' is not registered.");
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.NotEqual(toolName, tool.Title);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.NotEqual(toolName, tool.Description);
        Assert.False(string.IsNullOrWhiteSpace(tool.Category));
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
    }

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools()
    {
        var services = new ServiceCollection();

        // The tools are registered by the module's feature startups; run them all.
        foreach (var type in typeof(CrestApps.OrchardCore.AI.Agent.Startup).Assembly.GetTypes()
            .Where(type => typeof(StartupBase).IsAssignableFrom(type) && !type.IsAbstract))
        {
            var constructor = type.GetConstructors().Single();
            var arguments = constructor.GetParameters()
                .Select(parameter => Activator.CreateInstance(typeof(PassThroughLocalizer<>).MakeGenericType(parameter.ParameterType.GetGenericArguments()[0])))
                .ToArray();

            ((StartupBase)constructor.Invoke(arguments)).ConfigureServices(services);
        }

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
