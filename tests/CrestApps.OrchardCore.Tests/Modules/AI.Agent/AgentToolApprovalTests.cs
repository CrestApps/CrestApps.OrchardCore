using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Agent.Communications;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Agent.ContentTypes;
using CrestApps.OrchardCore.AI.Agent.Features;
using CrestApps.OrchardCore.AI.Agent.Recipes;
using CrestApps.OrchardCore.AI.Agent.Tenants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Agent;

/// <summary>
/// The agent tools that delete, disable, import or send ask the user before each call in a chat that can ask (the
/// AG-UI chat UI and admin widget). Read and create tools run without asking.
/// </summary>
public sealed class AgentToolApprovalTests
{
    [Theory]
    [InlineData(DeleteContentTool.TheName)]
    [InlineData(RemoveContentTypeDefinitionsTool.TheName)]
    [InlineData(RemoveContentPartDefinitionsTool.TheName)]
    [InlineData(DisableFeatureTool.TheName)]
    [InlineData(DisableTenantTool.TheName)]
    [InlineData(RemoveTenantTool.TheName)]
    [InlineData(SetupTenantTool.TheName)]
    [InlineData(ImportOrchardTool.TheName)]
    [InlineData(ExecuteStartupRecipesTool.TheName)]
    [InlineData(SendEmailTool.TheName)]
    [InlineData(SendSmsTool.TheName)]
    public void DestructiveTools_RequireApproval(string toolName)
    {
        Assert.True(GetTools()[toolName].RequiresApproval);
    }

    [Theory]
    [InlineData(GetContentTool.TheName)]
    [InlineData(ListContentTypesDefinitionsTool.TheName)]
    public void ReadTools_RunWithoutAsking(string toolName)
    {
        Assert.False(GetTools()[toolName].RequiresApproval);
    }

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools()
    {
        var services = new ServiceCollection();

        // The tools are registered by the module's feature startups (Contents, Tenants, Recipes...); run them all.
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
