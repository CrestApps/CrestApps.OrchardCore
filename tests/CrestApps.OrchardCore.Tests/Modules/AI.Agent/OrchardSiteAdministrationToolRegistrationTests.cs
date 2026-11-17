using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Agent.Admin;
using CrestApps.OrchardCore.AI.Agent.AdminDashboard;
using CrestApps.OrchardCore.AI.Agent.AdminMenus;
using CrestApps.OrchardCore.AI.Agent.Deployment;
using CrestApps.OrchardCore.AI.Agent.Settings;
using CrestApps.OrchardCore.AI.Agent.TenantFeatureProfiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Agent;

/// <summary>
/// The Orchard Core site administration tools of the AI Agent module (settings, admin pages, dashboard, admin menus,
/// tenant feature profiles and deployment plans) are registered with a title, a description and a category, can be
/// picked on a profile, and ask for approval exactly where the tool coverage plan says so. The tools this batch
/// deferred are not registered.
/// </summary>
public sealed class OrchardSiteAdministrationToolRegistrationTests
{
    private static readonly string[] _toolNames =
    [
        ListSiteSettingsGroupsTool.TheName,
        GetSiteSettingsTool.TheName,
        UpdateSiteSettingsTool.TheName,
        FindAdminPageTool.TheName,
        ListAdminDashboardWidgetsTool.TheName,
        ListAdminMenusTool.TheName,
        SaveAdminMenuTool.TheName,
        ListTenantFeatureProfilesTool.TheName,
        SaveTenantFeatureProfileTool.TheName,
        ListDeploymentPlansTool.TheName,
        SaveDeploymentPlanTool.TheName,
        DeleteDeploymentPlanTool.TheName,
        ExportDeploymentPlanTool.TheName,
    ];

    public static TheoryData<string, bool> Tools => new()
    {
        { ListSiteSettingsGroupsTool.TheName, false },
        { GetSiteSettingsTool.TheName, false },
        { UpdateSiteSettingsTool.TheName, true },
        { FindAdminPageTool.TheName, false },
        { ListAdminDashboardWidgetsTool.TheName, false },
        { ListAdminMenusTool.TheName, false },
        { SaveAdminMenuTool.TheName, false },
        { ListTenantFeatureProfilesTool.TheName, false },
        { SaveTenantFeatureProfileTool.TheName, false },
        { ListDeploymentPlansTool.TheName, false },
        { SaveDeploymentPlanTool.TheName, false },
        { DeleteDeploymentPlanTool.TheName, true },
        { ExportDeploymentPlanTool.TheName, true },
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

    [Theory]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.SettingsToolsStartup), 3)]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.AdminToolsStartup), 1)]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.AdminDashboardToolsStartup), 1)]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.AdminMenuToolsStartup), 2)]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.TenantFeatureProfilesToolsStartup), 2)]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.DeploymentToolsStartup), 4)]
    public void EachFeatureStartup_RegistersItsTools(Type startupType, int expectedCount)
    {
        // Act
        var tools = GetTools(startupType);

        // Assert
        Assert.Equal(expectedCount, tools.Count);
        Assert.All(tools.Keys, name => Assert.Contains(name, _toolNames));
    }

    [Theory]
    [InlineData("arrangeAdminDashboardWidgets")]
    [InlineData("deleteAdminMenu")]
    [InlineData("updateTenant")]
    [InlineData("getTenantSetupLink")]
    [InlineData("deleteTenantFeatureProfile")]
    public void ADeferredTool_IsNotRegistered(string toolName)
    {
        // Arrange
        var tools = GetTools();

        // Act
        var registered = tools.ContainsKey(toolName);

        // Assert
        Assert.False(registered);
    }

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools(Type startupType = null)
    {
        var services = new ServiceCollection();

        // The tools are registered by the module's feature startups; run them all, or the one that is asked for.
        foreach (var type in typeof(CrestApps.OrchardCore.AI.Agent.Startup).Assembly.GetTypes()
            .Where(type => typeof(StartupBase).IsAssignableFrom(type) && !type.IsAbstract)
            .Where(type => startupType is null || type == startupType))
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
