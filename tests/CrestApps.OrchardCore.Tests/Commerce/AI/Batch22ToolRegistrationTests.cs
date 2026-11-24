using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Commerce.AI;
using CrestApps.OrchardCore.Inventory.AI;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Products;
using CrestApps.OrchardCore.Products.AI;
using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using CommerceAIToolsStartup = CrestApps.OrchardCore.Commerce.AIToolsStartup;
using InventoryAIToolsStartup = CrestApps.OrchardCore.Inventory.AIToolsStartup;
using ProductsAIToolsStartup = CrestApps.OrchardCore.Products.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.Commerce.AI;

/// <summary>
/// The tools of batch 22 (store setup, products, product feeds, inventory and stock locations) are registered with a
/// title, a description and a category, can be picked on a profile, ask for approval as the catalog says (running a setup
/// step, the feed key, deleting a stock location), and are registered by the startup of the feature they belong to, which
/// requires the AI feature. Currencies are a record kind of the generic record tools (see CurrencyRecordKindTests).
/// </summary>
public sealed class Batch22ToolRegistrationTests
{
    private static readonly Type[] _startups =
    [
        typeof(CommerceAIToolsStartup),
        typeof(ProductsAIToolsStartup),
        typeof(FeedsAIToolsStartup),
        typeof(InventoryAIToolsStartup),
    ];

    public static TheoryData<string, bool> Tools => new()
    {
        { GetStoreSetupGuideTool.TheName, false },
        { RunStoreSetupStepTool.TheName, true },
        { ListProductsTool.TheName, false },
        { SaveProductVariantsTool.TheName, false },
        { UpdateProductPricingTool.TheName, false },
        { GetProductFeedStatusTool.TheName, false },
        { RegenerateProductFeedKeyTool.TheName, true },
        { ListStockItemsTool.TheName, false },
        { GetStockItemTool.TheName, false },
        { AdjustStockTool.TheName, false },
        { TransferStockTool.TheName, false },
        { ImportStockCountsTool.TheName, false },
        { SyncInventoryTool.TheName, false },
        { ListStockLocationsTool.TheName, false },
        { SaveStockLocationTool.TheName, false },
        { DeleteStockLocationTool.TheName, true },
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
    public void TheBatch_RegistersSixteenTools()
    {
        // Act
        var tools = GetTools(_startups);

        // Assert
        Assert.Equal(16, tools.Count);
    }

    [Theory]
    [InlineData(typeof(CommerceAIToolsStartup), 2, CommerceConstants.Features.Area)]
    [InlineData(typeof(ProductsAIToolsStartup), 3, ProductConstants.Feature.ModuleId)]
    [InlineData(typeof(FeedsAIToolsStartup), 2, ProductConstants.Feature.Feeds)]
    [InlineData(typeof(InventoryAIToolsStartup), 9, InventoryConstants.Features.Inventory)]
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
