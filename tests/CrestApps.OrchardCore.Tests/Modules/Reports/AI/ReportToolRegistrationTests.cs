using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.AI;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.AI;

/// <summary>
/// The Reports and Report Builder tools are registered with a title, a description and a category, can be picked on a
/// profile, ask for approval as the catalog says, and are registered by the startup of the feature they belong to, which
/// requires the AI feature.
/// </summary>
public sealed class ReportToolRegistrationTests
{
    public static TheoryData<string, bool> Tools => new()
    {
        { ListReportsTool.TheName, false },
        { GetReportTool.TheName, false },
        { RunReportTool.TheName, false },
        { ExportReportTool.TheName, false },
        { ListReportDataSetsTool.TheName, false },
        { GetReportDataSetSchemaTool.TheName, false },
        { QueryReportDataTool.TheName, false },
        { SaveReportDesignTool.TheName, false },
        { ShareReportDesignTool.TheName, true },
        { RestoreReportDesignVersionTool.TheName, false },
        { DeleteReportDesignTool.TheName, true },
        { CreateReportShareLinkTool.TheName, true },
        { RevokeReportShareLinkTool.TheName, true },
        { ListReportViewsTool.TheName, false },
        { SaveReportViewTool.TheName, false },
        { RefreshReportViewTool.TheName, false },
        { DeleteReportViewTool.TheName, true },
        { ListReportGroupsTool.TheName, false },
        { SaveReportGroupTool.TheName, false },
        { DeleteReportGroupTool.TheName, true },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName, bool requiresApproval)
    {
        // Arrange
        var tools = GetTools([typeof(AIToolsStartup), typeof(BuilderAIToolsStartup)]);

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
    [InlineData(typeof(AIToolsStartup), 4, ReportsConstants.Feature)]
    [InlineData(typeof(BuilderAIToolsStartup), 16, ReportsConstants.BuilderFeature)]
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
    public void TheExportFilterPrefix_IsTheReportFilterTypeName()
    {
        // The report page's date range filter fields are prefixed with the filter model's type name, which the export
        // address the tool builds relies on.
        Assert.Equal(nameof(CrestApps.OrchardCore.Reports.Models.ReportFilter), ExportReportTool.FilterFieldPrefix);
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
