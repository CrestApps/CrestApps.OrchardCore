using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.ContentAccessControl.AI;
using CrestApps.OrchardCore.Seo.AI;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using CrestApps.OrchardCore.TenantHierarchy;
using CrestApps.OrchardCore.TenantHierarchy.AI;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using ContentAccessAIToolsStartup = CrestApps.OrchardCore.ContentAccessControl.AIToolsStartup;
using SeoAIToolsStartup = CrestApps.OrchardCore.Seo.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy.AI;

/// <summary>
/// The tools of batch 18 (user name and avatar, content access, structured data, tenant hierarchy) are registered with a
/// title, a description and a category, can be picked on a profile, ask for approval as the catalog says (every write
/// that changes who may see content or enter a tenant, or creates, changes or removes a tenant), and are registered by
/// the startup of the feature they belong to, which requires the AI feature.
/// </summary>
public sealed class Batch18ToolRegistrationTests
{
    private static readonly Type[] _startups =
    [
        typeof(DisplayNameAIToolsStartup),
        typeof(AvatarAIToolsStartup),
        typeof(ContentAccessAIToolsStartup),
        typeof(SeoAIToolsStartup),
        typeof(ParentAIToolsStartup),
        typeof(PlatformAIToolsStartup),
    ];

    public static TheoryData<string, bool> Tools => new()
    {
        { SetUserDisplayNameTool.TheName, false },
        { SetUserAvatarTool.TheName, false },
        { GetContentItemAccessRolesTool.TheName, false },
        { SetContentItemAccessRolesTool.TheName, true },
        { GetContentItemStructuredDataTool.TheName, false },
        { ListChildTenantsTool.TheName, false },
        { SaveChildTenantTool.TheName, true },
        { ChangeChildTenantStateTool.TheName, true },
        { RemoveChildTenantTool.TheName, true },
        { RestoreChildTenantTool.TheName, true },
        { RetryChildTenantSetupTool.TheName, true },
        { DiscardChildTenantTool.TheName, true },
        { ListChildTenantFeaturesTool.TheName, false },
        { SetChildTenantFeaturesTool.TheName, true },
        { ListChildTenantAccessGrantsTool.TheName, false },
        { GrantChildTenantAccessTool.TheName, true },
        { RevokeChildTenantAccessTool.TheName, true },
        { GetTenantHierarchyOverviewTool.TheName, false },
        { SaveParentTenantTool.TheName, true },
        { ChangeParentTenantStateTool.TheName, true },
        { UnmakeParentTenantTool.TheName, true },
        { RemoveParentTenantTool.TheName, true },
        { MoveChildTenantTool.TheName, true },
        { DetachChildTenantTool.TheName, true },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName, bool requiresApproval)
    {
        // Arrange
        var tools = GetTools(_startups);

        // Act
        var found = tools.TryGetValue(toolName, out var tool);

        // Assert
        Assert.True(found, $"The tool '{toolName}' is not registered.");
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.NotEqual(toolName, tool.Title);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.False(string.IsNullOrWhiteSpace(tool.Category));
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
    }

    [Theory]
    [InlineData(typeof(DisplayNameAIToolsStartup), 1, UsersConstants.Feature.DisplayName)]
    [InlineData(typeof(AvatarAIToolsStartup), 1, UsersConstants.Feature.Avatars)]
    [InlineData(typeof(ContentAccessAIToolsStartup), 2, null)]
    [InlineData(typeof(SeoAIToolsStartup), 1, null)]
    [InlineData(typeof(ParentAIToolsStartup), 12, TenantHierarchyConstants.Features.Parent)]
    [InlineData(typeof(PlatformAIToolsStartup), 7, TenantHierarchyConstants.Features.Platform)]
    public void EachStartup_RegistersItsTools_ForItsFeature_WhenTheAIFeatureIsOn(Type startupType, int expectedCount, string featureId)
    {
        // Act
        var tools = GetTools([startupType]);
        var required = startupType.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false)
            .Cast<RequireFeaturesAttribute>()
            .Single();
        var feature = startupType.GetCustomAttributes(typeof(FeatureAttribute), inherit: false)
            .Cast<FeatureAttribute>()
            .SingleOrDefault();

        // Assert
        Assert.Equal(expectedCount, tools.Count);
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
        Assert.Equal(featureId, feature?.FeatureName);
    }

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools(IEnumerable<Type> startupTypes)
    {
        var services = new ServiceCollection();

        foreach (var type in startupTypes)
        {
            var constructor = type.GetConstructors().Single();
            var arguments = constructor.GetParameters()
                .Select(parameter => Activator.CreateInstance(typeof(PassThroughStringLocalizer<>).MakeGenericType(parameter.ParameterType.GetGenericArguments()[0])))
                .ToArray();

            ((StartupBase)constructor.Invoke(arguments)).ConfigureServices(services);
        }

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;
    }
}
