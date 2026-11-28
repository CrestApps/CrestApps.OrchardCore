using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.Agreements;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.WorkOrders;

/// <summary>
/// The work order and service agreement AI tools are registered by the Work Orders module, on the feature of the page
/// each mirrors, with a title, a description and the Field Service category, and ask for approval where they close or
/// complete work, delete, or can lead to customer notices. The seven field service tools Dispatch registers are unchanged.
/// </summary>
public sealed class WorkOrderAIToolRegistrationTests
{
    internal static readonly (string Name, bool RequiresApproval, string Permission, Type Startup)[] NewTools =
    [
        (ListWorkOrdersTool.ToolName, false, WorkOrdersPermissions.ViewWorkOrders.Name, typeof(WorkOrdersAIToolsStartup)),
        (UpdateWorkOrderTool.ToolName, false, WorkOrdersPermissions.ManageWorkOrders.Name, typeof(WorkOrdersAIToolsStartup)),
        (SetWorkOrderStatusTool.ToolName, false, WorkOrdersPermissions.ManageWorkOrders.Name, typeof(WorkOrdersAIToolsStartup)),
        (CloseWorkOrderTool.ToolName, true, WorkOrdersPermissions.ManageWorkOrders.Name, typeof(WorkOrdersAIToolsStartup)),
        (RecordWorkOrderPartUsedTool.ToolName, false, WorkOrdersPermissions.ManageWorkOrders.Name, typeof(WorkOrdersAIToolsStartup)),
        (AnswerWorkOrderChecklistTool.ToolName, false, WorkOrdersPermissions.ManageWorkOrders.Name, typeof(WorkOrdersAIToolsStartup)),
        (CompleteWorkOrderTool.ToolName, true, WorkOrdersPermissions.ManageWorkOrders.Name, typeof(WorkOrdersAIToolsStartup)),
        (ListWorkOrderSettingsTool.ToolName, false, WorkOrdersPermissions.ManageWorkOrderSettings.Name, typeof(WorkOrdersAIToolsStartup)),
        (GetWorkOrderSettingTool.ToolName, false, WorkOrdersPermissions.ManageWorkOrderSettings.Name, typeof(WorkOrdersAIToolsStartup)),
        (SaveWorkOrderSettingTool.ToolName, false, WorkOrdersPermissions.ManageWorkOrderSettings.Name, typeof(WorkOrdersAIToolsStartup)),
        (DeleteWorkOrderSettingTool.ToolName, true, WorkOrdersPermissions.ManageWorkOrderSettings.Name, typeof(WorkOrdersAIToolsStartup)),
        (GetServiceAgreementTool.ToolName, false, AgreementsPermissions.ViewAgreements.Name, typeof(ServiceAgreementsAIToolsStartup)),
        (SaveServiceAgreementTool.ToolName, true, AgreementsPermissions.ManageAgreements.Name, typeof(ServiceAgreementsAIToolsStartup)),
        (RunServiceAgreementTool.ToolName, true, AgreementsPermissions.ManageAgreements.Name, typeof(ServiceAgreementsAIToolsStartup)),
        (DeleteServiceAgreementTool.ToolName, true, AgreementsPermissions.ManageAgreements.Name, typeof(ServiceAgreementsAIToolsStartup)),
    ];

    public static TheoryData<string, bool> Tools
    {
        get
        {
            var data = new TheoryData<string, bool>();

            foreach (var (name, requiresApproval, _, _) in NewTools)
            {
                data.Add(name, requiresApproval);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName, bool requiresApproval)
    {
        // Arrange
        var tools = GetTools(NewTools.Single(tool => tool.Name == toolName).Startup);

        // Act
        var found = tools.TryGetValue(toolName, out var tool);

        // Assert
        Assert.True(found, $"The tool '{toolName}' is not registered.");
        Assert.Equal(toolName, tool.Name);
        Assert.Equal(toolName, Create(toolName).Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.NotEqual(toolName, tool.Title);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.False(string.IsNullOrWhiteSpace(Create(toolName).Description));
        Assert.Equal("Field Service", tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
    }

    [Fact]
    public void Startups_RegisterFifteenToolsWithSixApprovals_OnTheirFeatures_AndTheDispatchToolsAreUnchanged()
    {
        // Act
        var workOrderTools = GetTools(typeof(WorkOrdersAIToolsStartup));
        var agreementTools = GetTools(typeof(ServiceAgreementsAIToolsStartup));
        var dispatchTools = GetTools(typeof(CrestApps.OrchardCore.Dispatch.AIToolsStartup));

        // Assert
        Assert.Equal(11, workOrderTools.Count);
        Assert.Equal(4, agreementTools.Count);
        Assert.Equal(6, workOrderTools.Values.Concat(agreementTools.Values).Count(tool => tool.RequiresApproval));
        Assert.Equal(WorkOrdersConstants.Features.WorkOrders, FeatureOf(typeof(WorkOrdersAIToolsStartup)));
        Assert.Equal(WorkOrdersConstants.Features.Agreements, FeatureOf(typeof(ServiceAgreementsAIToolsStartup)));

        foreach (var startup in new[] { typeof(WorkOrdersAIToolsStartup), typeof(ServiceAgreementsAIToolsStartup) })
        {
            var required = startup.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();

            Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
        }

        Assert.Equal(
            ["dispatchFindTechnician", "dispatchOptimizeAndExplain", "dispatchRescheduleVisit", "dispatchWhereIsMyTechnician", "workOrderCreate", "workOrderStatus", "workOrderSummarize"],
            dispatchTools.Keys.Order(StringComparer.Ordinal));
        Assert.Empty(workOrderTools.Keys.Concat(agreementTools.Keys).Intersect(dispatchTools.Keys));
        Assert.DoesNotContain("addWorkOrderNote", workOrderTools.Keys);
        Assert.DoesNotContain("createFollowUpWorkOrder", workOrderTools.Keys);
    }

    internal static AIFunction Create(string toolName)
        => toolName switch
        {
            ListWorkOrdersTool.ToolName => new ListWorkOrdersTool(),
            UpdateWorkOrderTool.ToolName => new UpdateWorkOrderTool(),
            SetWorkOrderStatusTool.ToolName => new SetWorkOrderStatusTool(),
            CloseWorkOrderTool.ToolName => new CloseWorkOrderTool(),
            RecordWorkOrderPartUsedTool.ToolName => new RecordWorkOrderPartUsedTool(),
            AnswerWorkOrderChecklistTool.ToolName => new AnswerWorkOrderChecklistTool(),
            CompleteWorkOrderTool.ToolName => new CompleteWorkOrderTool(),
            ListWorkOrderSettingsTool.ToolName => new ListWorkOrderSettingsTool(),
            GetWorkOrderSettingTool.ToolName => new GetWorkOrderSettingTool(),
            SaveWorkOrderSettingTool.ToolName => new SaveWorkOrderSettingTool(),
            DeleteWorkOrderSettingTool.ToolName => new DeleteWorkOrderSettingTool(),
            GetServiceAgreementTool.ToolName => new GetServiceAgreementTool(),
            SaveServiceAgreementTool.ToolName => new SaveServiceAgreementTool(),
            RunServiceAgreementTool.ToolName => new RunServiceAgreementTool(),
            DeleteServiceAgreementTool.ToolName => new DeleteServiceAgreementTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "Unknown tool."),
        };

    private static string FeatureOf(Type startupType)
        => startupType.GetCustomAttributes(typeof(FeatureAttribute), inherit: false).Cast<FeatureAttribute>().Single().FeatureName;

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools(Type startupType)
    {
        var services = new ServiceCollection();
        var localizer = Activator.CreateInstance(typeof(PassThroughLocalizer<>).MakeGenericType(startupType));
        var startup = (StartupBase)Activator.CreateInstance(startupType, localizer);

        startup.ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
