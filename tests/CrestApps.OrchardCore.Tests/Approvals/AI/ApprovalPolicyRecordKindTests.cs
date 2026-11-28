using System.Text.Json;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Approvals;
using CrestApps.OrchardCore.Approvals.AI;
using CrestApps.OrchardCore.Approvals.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using ApprovalsAIToolsStartup = CrestApps.OrchardCore.Approvals.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.Approvals.AI;

/// <summary>
/// Approval policies are the read-only <c>approvalPolicy</c> record kind, under Manage approval policies: their editor
/// posts stages with lists of approver rules, which the editor replay cannot post. The kind shows a policy saved before
/// stages as its one stage, as the editor does.
/// </summary>
public sealed class ApprovalPolicyRecordKindTests
{
    [Fact]
    public void Startup_RegistersThreeTools_AndAReadOnlyApprovalPolicyKind_OnTheApprovalsFeature()
    {
        // Arrange
        var services = new ServiceCollection();

        new ApprovalsAIToolsStartup(new CrestApps.OrchardCore.Tests.Modules.Reports.Designer.PassThroughStringLocalizer<ApprovalsAIToolsStartup>()).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        // Act
        var tools = ApprovalsAIToolHarness.GetTools();
        var kind = Assert.Single(provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds.Values);
        var required = typeof(ApprovalsAIToolsStartup).GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();
        var feature = typeof(ApprovalsAIToolsStartup).GetCustomAttributes(typeof(FeatureAttribute), inherit: false).Cast<FeatureAttribute>().Single();

        // Assert
        Assert.Equal(3, tools.Count);
        Assert.Contains(ListApprovalRequestsTool.TheName, tools.Keys);
        Assert.Contains(GetApprovalRequestTool.TheName, tools.Keys);
        Assert.Contains(DecideApprovalRequestTool.TheName, tools.Keys);
        Assert.Equal("approvalPolicy", kind.Name);
        Assert.False(kind.CanWrite);
        Assert.Equal(ApprovalsPermissions.ManageApprovalPolicies, kind.Permission);
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
        Assert.Equal(ApprovalsConstants.Features.Approvals, feature.FeatureName);
    }

    [Fact]
    public void Projection_ShowsAPolicySavedBeforeStages_AsItsOneStage()
    {
        // Arrange
        var policy = new ApprovalPolicy
        {
            ItemId = "policy-1",
            Name = "ExpenseApproval",
            Title = "Expense approval",
            RequiredApprovals = 2,
            Approvers = [new ApproverRule { Kind = ApproverRuleKinds.Role, Value = "Finance" }],
            MinimumAmount = 500m,
            Currency = "USD",
            DueAfterHours = 48,
        };

        // Act
        var json = Project(policy);
        var stage = Assert.Single(json.GetProperty("stages").EnumerateArray());
        var rule = Assert.Single(stage.GetProperty("approvers").EnumerateArray());

        // Assert
        Assert.Equal("policy-1", json.GetProperty("id").GetString());
        Assert.Equal("Expense approval", json.GetProperty("title").GetString());
        Assert.True(json.GetProperty("general").GetBoolean());
        Assert.Equal(500m, json.GetProperty("minimumAmount").GetDecimal());
        Assert.Equal("USD", json.GetProperty("currency").GetString());
        Assert.Equal(48, json.GetProperty("dueAfterHours").GetInt32());
        Assert.Equal(2, stage.GetProperty("requiredApprovals").GetInt32());
        Assert.Equal(ApproverRuleKinds.Role, rule.GetProperty("kind").GetString());
        Assert.Equal("Finance", rule.GetProperty("value").GetString());
        Assert.False(json.TryGetProperty("properties", out _));
        Assert.False(json.TryGetProperty("modifiedUtc", out _));
    }

    [Fact]
    public void Projection_ShowsATypedPolicysStagesInOrder()
    {
        // Arrange
        var policy = new ApprovalPolicy
        {
            ItemId = "policy-2",
            Name = "PayRunApproval",
            Type = "PayRun",
            Stages =
            [
                new ApprovalStage { Title = "Manager", Approvers = [new ApproverRule { Kind = ApproverRuleKinds.Manager }] },
                new ApprovalStage { Title = "Finance", RequiredApprovals = 2, Approvers = [new ApproverRule { Kind = ApproverRuleKinds.Permission, Value = "ApprovePayRuns" }] },
            ],
        };

        // Act
        var json = Project(policy);
        var stages = json.GetProperty("stages").EnumerateArray().ToList();

        // Assert
        Assert.Equal("PayRun", json.GetProperty("type").GetString());
        Assert.False(json.TryGetProperty("general", out var general) && general.ValueKind == JsonValueKind.True);
        Assert.Equal(new[] { "Manager", "Finance" }, stages.Select(stage => stage.GetProperty("title").GetString()).ToArray());
        Assert.Equal(2, stages[1].GetProperty("requiredApprovals").GetInt32());
    }

    private static JsonElement Project(ApprovalPolicy policy)
        => JsonSerializer.SerializeToElement(ApprovalsAIToolsStartup.ProjectPolicy(policy), JsonSerializerOptions.Web);
}
