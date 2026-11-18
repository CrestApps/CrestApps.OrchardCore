using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.Skills.AI;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.AI;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using SkillsAIToolsStartup = CrestApps.OrchardCore.Skills.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.Staff.AI;

/// <summary>
/// The tools of batch 21 (skills and certificates, staff records, badges and capabilities) are registered with a title, a
/// description and a category, can be picked on a profile, ask for approval as the catalog says (the deletes and the
/// badge PIN), and are registered by the startup of the feature they belong to, which requires the AI feature.
/// <c>setStaffPay</c> is deferred and must not be registered.
/// </summary>
public sealed class Batch21ToolRegistrationTests
{
    private static readonly Type[] _startups =
    [
        typeof(SkillsAIToolsStartup),
        typeof(StaffManagementAIToolsStartup),
        typeof(BadgesAIToolsStartup),
    ];

    public static TheoryData<string, bool> Tools => new()
    {
        { ListSkillsTool.TheName, false },
        { SaveSkillTool.TheName, false },
        { DeleteSkillTool.TheName, true },
        { ListCertificationTypesTool.TheName, false },
        { SaveCertificationTypeTool.TheName, false },
        { DeleteCertificationTypeTool.TheName, true },
        { GetSubjectSkillsTool.TheName, false },
        { SaveSkillAssignmentTool.TheName, false },
        { RemoveSkillAssignmentTool.TheName, false },
        { FindSkillHoldersTool.TheName, false },
        { ListCertificatesTool.TheName, false },
        { SaveCertificateTool.TheName, false },
        { SetCertificateStatusTool.TheName, false },
        { DeleteCertificateTool.TheName, true },
        { GetStaffMemberTool.TheName, false },
        { SaveStaffMemberTool.TheName, false },
        { DeleteStaffMemberTool.TheName, true },
        { SetStaffBadgePinTool.TheName, true },
        { ListStaffCapabilitiesTool.TheName, false },
        { SaveStaffCapabilityTool.TheName, false },
        { DeleteStaffCapabilityTool.TheName, true },
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

    [Fact]
    public void TheBatch_RegistersTwentyOneTools_AndNoPayTool()
    {
        // Act
        var tools = GetTools(_startups);

        // Assert
        Assert.Equal(21, tools.Count);
        Assert.DoesNotContain("setStaffPay", tools.Keys);
    }

    [Theory]
    [InlineData(typeof(SkillsAIToolsStartup), 14, null)]
    [InlineData(typeof(StaffManagementAIToolsStartup), 6, null)]
    [InlineData(typeof(BadgesAIToolsStartup), 1, StaffConstants.Features.Badges)]
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
