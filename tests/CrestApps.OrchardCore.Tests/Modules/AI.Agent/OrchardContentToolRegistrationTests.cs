using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Agent.ContentLocalization;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Agent.ContentTypes;
using CrestApps.OrchardCore.AI.Agent.Flows;
using CrestApps.OrchardCore.AI.Agent.PublishLater;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Agent;

/// <summary>
/// The Orchard Core content tools of the AI Agent module (content items, content types, localization, nested items and
/// scheduling) are registered with a title, a description and a category, can be picked on a profile, and ask for
/// approval exactly where the tool coverage plan says so.
/// </summary>
public sealed class OrchardContentToolRegistrationTests
{
    public static TheoryData<string, bool> Tools => new()
    {
        { GetContentItemTool.TheName, false },
        { ListContentItemsTool.TheName, false },
        { ListContentItemVersionsTool.TheName, false },
        { CompareContentItemVersionsTool.TheName, false },
        { RestoreContentItemVersionTool.TheName, false },
        { DiscardContentItemDraftTool.TheName, true },
        { ChangeContentItemOwnerTool.TheName, true },
        { SaveContentTypePartTool.TheName, false },
        { RemoveContentTypePartTool.TheName, true },
        { SaveContentPartFieldTool.TheName, false },
        { RemoveContentPartFieldTool.TheName, true },
        { ListShapeAlternatesTool.TheName, false },
        { LocalizeContentItemTool.TheName, false },
        { SaveNestedContentItemTool.TheName, false },
        { RemoveNestedContentItemTool.TheName, true },
        { ScheduleContentItemTool.TheName, false },
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
