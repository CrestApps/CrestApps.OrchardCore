using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.Accounts.B2B.AI;
using CrestApps.OrchardCore.Customers;
using CrestApps.OrchardCore.Customers.AI;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using B2BAIToolsStartup = CrestApps.OrchardCore.Accounts.B2B.AIToolsStartup;
using CustomersAIToolsStartup = CrestApps.OrchardCore.Customers.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.Customers.AI;

/// <summary>
/// The tools of batch 20 (business buying and price lists, customers, store credit, loyalty points) are registered with a
/// title, a description and a category, can be picked on a profile, ask for approval as the catalog says (punchout,
/// deletes, data requests, store credit and points), and are registered by the startup of the feature they belong to,
/// which requires the AI feature. The teams became a record kind; the Teams tests check its registration.
/// </summary>
public sealed class Batch20ToolRegistrationTests
{
    private static readonly Type[] _startups =
    [
        typeof(B2BAIToolsStartup),
        typeof(CustomersAIToolsStartup),
        typeof(StoreCreditAIToolsStartup),
        typeof(LoyaltyAIToolsStartup),
    ];

    public static TheoryData<string, bool> Tools => new()
    {
        { GetB2BAccountSummaryTool.TheName, false },
        { SaveB2BAccountSettingsTool.TheName, true },
        { ConfigureB2BPunchoutTool.TheName, true },
        { ListPriceListsTool.TheName, false },
        { SavePriceListTool.TheName, false },
        { DeletePriceListTool.TheName, true },
        { ListCustomersTool.TheName, false },
        { GetCustomerTool.TheName, false },
        { SaveCustomerTool.TheName, false },
        { SaveCustomerAddressTool.TheName, false },
        { DeleteCustomerAddressTool.TheName, true },
        { ExportCustomerDataTool.TheName, true },
        { EraseCustomerDataTool.TheName, true },
        { AdjustCustomerStoreCreditTool.TheName, true },
        { AdjustCustomerLoyaltyPointsTool.TheName, true },
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
    public void TheBatch_RegistersFifteenTools()
    {
        // Act
        var tools = GetTools(_startups);

        // Assert
        Assert.Equal(15, tools.Count);
    }

    [Theory]
    [InlineData(typeof(B2BAIToolsStartup), 6, null)]
    [InlineData(typeof(CustomersAIToolsStartup), 7, null)]
    [InlineData(typeof(StoreCreditAIToolsStartup), 1, CustomersConstants.Features.StoreCredit)]
    [InlineData(typeof(LoyaltyAIToolsStartup), 1, CustomersConstants.Features.Loyalty)]
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
