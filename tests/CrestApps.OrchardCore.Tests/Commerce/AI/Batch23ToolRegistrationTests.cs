using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.AI;
using CrestApps.OrchardCore.Inventory;
using CrestApps.OrchardCore.Inventory.AI;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Orders.AI;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using CheckoutAIToolsStartup = CrestApps.OrchardCore.Checkout.AIToolsStartup;
using OrdersAIToolsStartup = CrestApps.OrchardCore.Orders.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.Commerce.AI;

/// <summary>
/// The tools of batch 23 (purchase orders, coupons and orders) are registered with a title, a description and a
/// category, can be picked on a profile, ask for approval as the catalog and the owner's review say (canceling and
/// deleting a purchase order; saving, deleting and generating codes for a coupon; refunding, canceling, and emailing a
/// confirmation or an invoice), and are registered by the startup of the feature they belong to, which requires the AI
/// feature.
/// </summary>
public sealed class Batch23ToolRegistrationTests
{
    private static readonly Type[] _startups =
    [
        typeof(PurchaseOrdersAIToolsStartup),
        typeof(CheckoutAIToolsStartup),
        typeof(OrdersAIToolsStartup),
    ];

    public static TheoryData<string, bool> Tools => new()
    {
        { ListPurchaseOrdersTool.TheName, false },
        { GetPurchaseOrderTool.TheName, false },
        { SavePurchaseOrderTool.TheName, false },
        { MarkPurchaseOrderOrderedTool.TheName, false },
        { ReceivePurchaseOrderTool.TheName, false },
        { CancelPurchaseOrderTool.TheName, true },
        { DeletePurchaseOrderTool.TheName, true },
        { ListCouponsTool.TheName, false },
        { GetCouponTool.TheName, false },
        { SaveCouponTool.TheName, true },
        { DeleteCouponTool.TheName, true },
        { GenerateCouponCodesTool.TheName, true },
        { ListOrdersTool.TheName, false },
        { GetOrderTool.TheName, false },
        { AddOrderNoteTool.TheName, false },
        { RefundOrderTool.TheName, true },
        { CancelOrderTool.TheName, true },
        { ResendOrderConfirmationTool.TheName, true },
        { SaveDraftOrderTool.TheName, false },
        { SendOrderInvoiceTool.TheName, true },
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
    public void TheBatch_RegistersTwentyTools()
    {
        // Act
        var tools = GetTools(_startups);

        // Assert
        Assert.Equal(20, tools.Count);
    }

    [Theory]
    [InlineData(typeof(PurchaseOrdersAIToolsStartup), 7, InventoryConstants.Features.PurchaseOrders)]
    [InlineData(typeof(CheckoutAIToolsStartup), 5, CheckoutConstants.Features.Area)]
    [InlineData(typeof(OrdersAIToolsStartup), 8, OrderConstants.Features.Orders)]
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
