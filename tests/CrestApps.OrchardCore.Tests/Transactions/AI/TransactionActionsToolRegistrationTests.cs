using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.AI;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Transactions.AI;

/// <summary>
/// The transaction actions are registered with a title, a description and the Payments category, can be picked on a
/// profile, and ask for approval when they move money or credit or can't be undone (taking an entry back, paying with
/// credit, adjusting, canceling a scheduled card charge); the preview and the collection settings switch do not ask.
/// </summary>
public sealed class TransactionActionsToolRegistrationTests
{
    public static TheoryData<string, bool> Tools => new()
    {
        { PreviewTransactionActionTool.TheName, false },
        { ReverseTransactionEntryTool.TheName, true },
        { ApplyTransactionCreditTool.TheName, true },
        { AdjustTransactionTool.TheName, true },
        { CancelTransactionAutoChargeTool.TheName, true },
        { SetPartyCollectionSettingsTool.TheName, false },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName, bool requiresApproval)
    {
        // Act
        var found = TransactionActionToolContext.GetTools().TryGetValue(toolName, out var tool);

        // Assert
        Assert.True(found, $"The tool '{toolName}' is not registered.");
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.NotEqual(toolName, tool.Title);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Payments", tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
    }

    [Theory]
    [InlineData(typeof(TransactionActionsAIToolsStartup), TransactionsConstants.Features.Area)]
    [InlineData(typeof(TransactionCreditAIToolsStartup), TransactionsConstants.Features.Area)]
    [InlineData(typeof(PaymentTermsAIToolsStartup), TransactionsConstants.Features.PaymentTerms)]
    public void TheStartups_BelongToTheirFeature_AndNeedTheAIFeature(Type startupType, string featureId)
    {
        // Act
        var feature = startupType.GetCustomAttributes(typeof(FeatureAttribute), inherit: false).Cast<FeatureAttribute>().Single();
        var required = startupType.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();

        // Assert
        Assert.Equal(featureId, feature.FeatureName);
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
    }
}
