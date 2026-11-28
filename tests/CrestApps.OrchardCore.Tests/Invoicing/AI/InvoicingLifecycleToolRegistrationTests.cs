using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// The invoice life-cycle tools are registered with a title, a description and a category, can be picked on a profile,
/// and ask for approval when they cannot be undone, move money or credit, or leave the site (issuing, deciding an
/// approval, voiding, writing off, recording a payment, e-mailing a statement); reading an invoice and previewing an
/// action change nothing and do not ask. The four tools main shipped stay as they were.
/// </summary>
public sealed class InvoicingLifecycleToolRegistrationTests
{
    public static TheoryData<string, bool> Tools => new()
    {
        { GetInvoiceTool.TheName, false },
        { IssueInvoiceTool.TheName, true },
        { DecideInvoiceApprovalTool.TheName, true },
        { PreviewInvoiceActionTool.TheName, false },
        { VoidInvoiceTool.TheName, true },
        { WriteOffInvoiceTool.TheName, true },
        { RecordInvoicePaymentTool.TheName, true },
        { SendInvoiceStatementTool.TheName, true },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName, bool requiresApproval)
    {
        // Arrange
        var tools = GetTools(typeof(InvoicingAIToolsStartup), typeof(InvoicingCollectionsAIToolsStartup));

        // Act
        var found = tools.TryGetValue(toolName, out var tool);

        // Assert
        Assert.True(found, $"The tool '{toolName}' is not registered.");
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.NotEqual(toolName, tool.Title);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Invoicing", tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
    }

    [Fact]
    public void TheInvoicingStartup_KeepsTheShippedTools_AndAddsSeven()
    {
        // Act
        var tools = GetTools(typeof(InvoicingAIToolsStartup));

        // Assert
        Assert.Equal(11, tools.Count);
        Assert.All(new[] { FindInvoicesTool.ToolName, SummarizeReceivablesTool.ToolName, DraftInvoiceTool.ToolName, SendInvoiceReminderTool.ToolName }, name => Assert.True(tools.ContainsKey(name), name));
    }

    [Fact]
    public void TheCollectionsStartup_RegistersTheStatementTool_ForTheCollectionsFeature_WhenTheAIFeatureIsOn()
    {
        // Act
        var tools = GetTools(typeof(InvoicingCollectionsAIToolsStartup));
        var required = typeof(InvoicingCollectionsAIToolsStartup).GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();
        var feature = typeof(InvoicingCollectionsAIToolsStartup).GetCustomAttributes(typeof(FeatureAttribute), inherit: false).Cast<FeatureAttribute>().Single();

        // Assert
        Assert.Equal(SendInvoiceStatementTool.TheName, Assert.Single(tools.Keys));
        Assert.Contains(AIConstants.Feature.Area, required.RequiredFeatureNames);
        Assert.Equal(InvoicingConstants.Features.Collections, feature.FeatureName);
    }

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools(params Type[] startupTypes)
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
