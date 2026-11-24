using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Markets;
using CrestApps.OrchardCore.Orders;
using CrestApps.OrchardCore.Orders.AI;
using CrestApps.OrchardCore.PointOfSale;
using CrestApps.OrchardCore.PointOfSale.AI;
using CrestApps.OrchardCore.Shipping;
using CrestApps.OrchardCore.Shipping.AI;
using CrestApps.OrchardCore.Stripe.AI;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using MarketsAIToolsStartup = CrestApps.OrchardCore.Markets.AIToolsStartup;
using PointOfSaleAIToolsStartup = CrestApps.OrchardCore.PointOfSale.AIToolsStartup;
using ShippingAIToolsStartup = CrestApps.OrchardCore.Shipping.AIToolsStartup;
using StripeAIToolsStartup = CrestApps.OrchardCore.Stripe.AIToolsStartup;
using TransactionsAIToolsStartup = CrestApps.OrchardCore.Transactions.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.Commerce.AI;

/// <summary>
/// The tools of the rest of commerce core (returns, gift cards, order diagnosis, shipments and labels, rates, Stripe,
/// transactions, the payment ledgers and register shifts) are registered with a title, a description and a category,
/// can be picked on a profile, ask for approval only when they send, move or record money for good, or cannot be undone,
/// and are registered by startups that require the AI feature. Markets, registers, departments, shipping zones, tax rules
/// and tax tables are read-only record kinds.
/// </summary>
public sealed class CommerceCoreRestToolRegistrationTests
{
    private static readonly Type[] _startups =
    [
        typeof(ReturnsAIToolsStartup),
        typeof(GiftCardsAIToolsStartup),
        typeof(OrderDiagnosticsAIToolsStartup),
        typeof(ShippingAIToolsStartup),
        typeof(StripeAIToolsStartup),
        typeof(TransactionsAIToolsStartup),
        typeof(PaymentLedgerAIToolsStartup),
        typeof(PointOfSaleAIToolsStartup),
        typeof(MarketsAIToolsStartup),
        typeof(TaxRulesAIToolsStartup),
    ];

    public static TheoryData<string, bool> Tools => new()
    {
        { ListOrderReturnsTool.TheName, false },
        { GetOrderReturnTool.TheName, false },
        { UpdateOrderReturnStatusTool.TheName, true },
        { ReceiveOrderReturnTool.TheName, false },
        { ListGiftCardsTool.TheName, false },
        { GetGiftCardTool.TheName, false },
        { IssueGiftCardTool.TheName, true },
        { UpdateGiftCardTool.TheName, false },
        { AdjustGiftCardBalanceTool.TheName, true },
        { EmailGiftCardTool.TheName, true },
        { DiagnoseOrderTool.TheName, false },
        { GetOrderFulfillmentTool.TheName, false },
        { CreateShipmentTool.TheName, false },
        { ShipShipmentTool.TheName, true },
        { UpdateShipmentStatusTool.TheName, false },
        { DeleteShipmentTool.TheName, true },
        { GetShipmentLabelRatesTool.TheName, false },
        { BuyShipmentLabelTool.TheName, true },
        { TestShippingRatesTool.TheName, false },
        { GetStripeConnectionStatusTool.TheName, false },
        { DisconnectStripeTool.TheName, true },
        { ListTransactionsTool.TheName, false },
        { GetTransactionTool.TheName, false },
        { RecordTransactionPaymentTool.TheName, true },
        { AdjustTransactionTool.TheName, true },
        { CancelTransactionTool.TheName, true },
        { SendTransactionNoticeTool.TheName, true },
        { AddTransactionNoteTool.TheName, false },
        { ListPaymentsTool.TheName, false },
        { RefundPaymentTool.TheName, true },
        { ListRefundsTool.TheName, false },
        { ResolveRefundTool.TheName, true },
        { ListPaymentIssuesTool.TheName, false },
        { ResolvePaymentIssueTool.TheName, false },
        { ListRegisterShiftsTool.TheName, false },
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
    public void TheArea_RegistersThirtyFiveTools()
    {
        // Act
        var tools = GetTools(_startups);

        // Assert
        Assert.Equal(35, tools.Count);
    }

    [Theory]
    [InlineData(typeof(ReturnsAIToolsStartup))]
    [InlineData(typeof(GiftCardsAIToolsStartup))]
    [InlineData(typeof(OrderDiagnosticsAIToolsStartup))]
    [InlineData(typeof(ShippingAIToolsStartup))]
    [InlineData(typeof(StripeAIToolsStartup))]
    [InlineData(typeof(TransactionsAIToolsStartup))]
    [InlineData(typeof(PaymentLedgerAIToolsStartup))]
    [InlineData(typeof(PointOfSaleAIToolsStartup))]
    [InlineData(typeof(MarketsAIToolsStartup))]
    [InlineData(typeof(TaxRulesAIToolsStartup))]
    public void EachStartup_HasAFeature_AndRequiresTheAIFeature(Type startupType)
    {
        // Act
        var required = startupType.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false)
            .Cast<RequireFeaturesAttribute>()
            .Single();
        var feature = startupType.GetCustomAttributes(typeof(FeatureAttribute), inherit: false)
            .Cast<FeatureAttribute>()
            .Single();

        // Assert
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
        Assert.False(string.IsNullOrWhiteSpace(feature.FeatureName));
    }

    [Fact]
    public void TheRecordKinds_AreReadOnly_WithTheirPagesPermission()
    {
        // Act
        var kinds = GetKinds(_startups);

        // Assert
        Assert.Equal(new[] { "market", "posDepartment", "posRegister", "shippingZone", "taxRule", "taxTable" }, kinds.Keys.Order(StringComparer.Ordinal));
        Assert.All(kinds.Values, kind =>
        {
            Assert.False(kind.CanWrite);
            Assert.False(string.IsNullOrWhiteSpace(kind.Description));
            Assert.NotEqual(kind.Name, kind.Title);
        });
        Assert.Equal(MarketsPermissions.ManageMarkets.Name, kinds["market"].Permission.Name);
        Assert.Equal(PosPermissions.ManageRegisters.Name, kinds["posRegister"].Permission.Name);
        Assert.Equal(PosPermissions.ManageRegisters.Name, kinds["posDepartment"].Permission.Name);
        Assert.Equal(ShippingPermissions.ManageShipping.Name, kinds["shippingZone"].Permission.Name);
        Assert.Equal(TaxationPermissions.ManageTaxation.Name, kinds["taxRule"].Permission.Name);
        Assert.Equal(TaxationPermissions.ManageTaxation.Name, kinds["taxTable"].Permission.Name);
    }

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools(IEnumerable<Type> startupTypes)
    {
        using var provider = Configure(startupTypes);

        return provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;
    }

    private static IReadOnlyDictionary<string, AIRecordKind> GetKinds(IEnumerable<Type> startupTypes)
    {
        using var provider = Configure(startupTypes);

        return provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds;
    }

    private static ServiceProvider Configure(IEnumerable<Type> startupTypes)
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

        return services.BuildServiceProvider();
    }
}
