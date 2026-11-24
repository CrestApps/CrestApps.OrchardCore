using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.AI;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using WebhooksAIToolsStartup = CrestApps.OrchardCore.Webhooks.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.Modules.Webhooks.AI;

/// <summary>
/// The report subscription and webhook tools are registered with a title, a description and a category, can be picked
/// on a profile, ask for approval as the catalog says (every write, send, ping and delete), and are registered by the
/// startup of the feature they belong to, which requires the AI feature.
/// </summary>
public sealed class Batch17ToolRegistrationTests
{
    public static TheoryData<string, bool> Tools => new()
    {
        { ListReportSubscriptionsTool.TheName, false },
        { SaveReportSubscriptionTool.TheName, true },
        { SendReportSubscriptionTool.TheName, true },
        { DeleteReportSubscriptionTool.TheName, true },
        { ListWebhooksTool.TheName, false },
        { ListWebhookEventsTool.TheName, false },
        { SaveWebhookTool.TheName, true },
        { RotateWebhookSecretTool.TheName, true },
        { DeleteWebhookTool.TheName, true },
        { ListWebhookDeliveriesTool.TheName, false },
        { SendWebhookDeliveryTool.TheName, true },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName, bool requiresApproval)
    {
        // Arrange
        var tools = GetTools([typeof(SubscriptionsAIToolsStartup), typeof(WebhooksAIToolsStartup)]);

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
    [InlineData(typeof(SubscriptionsAIToolsStartup), 4, ReportsConstants.SubscriptionsFeature)]
    [InlineData(typeof(WebhooksAIToolsStartup), 7, WebhooksConstants.Feature.Webhooks)]
    public void EachStartup_RegistersItsTools_ForItsFeature_WhenTheAIFeatureIsOn(Type startupType, int expectedCount, string featureId)
    {
        // Act
        var tools = GetTools([startupType]);
        var required = startupType.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false)
            .Cast<RequireFeaturesAttribute>()
            .Single();
        var feature = startupType.GetCustomAttributes(typeof(FeatureAttribute), inherit: false)
            .Cast<FeatureAttribute>()
            .Single();

        // Assert
        Assert.Equal(expectedCount, tools.Count);
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
        Assert.Equal(featureId, feature.FeatureName);
    }

    [Fact]
    public void TheWebhooksPermission_KeepsItsReleasedName()
        => Assert.Equal("ManageCommerceWebhooks", WebhookPermissions.ManageWebhooks.Name);

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
