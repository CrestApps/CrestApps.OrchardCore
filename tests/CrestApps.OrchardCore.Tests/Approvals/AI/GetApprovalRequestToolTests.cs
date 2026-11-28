using CrestApps.OrchardCore.Approvals;
using CrestApps.OrchardCore.Approvals.AI;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Tests.Doubles;

namespace CrestApps.OrchardCore.Tests.Approvals.AI;

/// <summary>
/// <c>getApprovalRequest</c> opens a request for the same people as its page: its approvers, its requester, those who
/// decided it, and those who see it through View approval requests and the subject type's access. Anyone else is told,
/// in the same words as for an id that does not exist, that there is no such request. It says whether the person may
/// decide or withdraw it, and returns the decisions with their notes in local time.
/// </summary>
public sealed class GetApprovalRequestToolTests
{
    [Fact]
    public async Task RefusesACallerWhoIsNotSignedIn_OrWithoutTheAdminPermission()
    {
        // Arrange
        var (context, payRun) = await SeedAsync();
        var arguments = new Dictionary<string, object> { ["requestId"] = payRun.ItemId };

        // Act
        var anonymous = await ApprovalsAIToolHarness.InvokeAsync(new GetApprovalRequestTool(), ApprovalsAIToolHarness.Services(context, ApprovalsAIToolHarness.Anonymous(), ApprovalsAIToolHarness.Admin), arguments);
        var noAdmin = await ApprovalsAIToolHarness.InvokeAsync(new GetApprovalRequestTool(), ApprovalsAIToolHarness.Services(context, TestSecurity.User("pat"), ApprovalsPermissions.ViewApprovalRequests), arguments);

        // Assert
        Assert.Equal(ApprovalsAIToolBase.NotAllowedMessage, anonymous);
        Assert.Equal(ApprovalsAIToolBase.NotAllowedMessage, noAdmin);
    }

    [Fact]
    public async Task ARequestThePersonMayNotOpen_IsAnsweredAsOneThatDoesNotExist()
    {
        // Arrange: no rule names Sam; the rule names Lee, but Lee may not act on group A's pay runs.
        var (context, payRun) = await SeedAsync();

        // Act
        var sam = await GetAsync(context, "sam", payRun.ItemId);
        var lee = await GetAsync(context, "lee", payRun.ItemId);
        var missing = await GetAsync(context, "pat", "no-such-request");

        // Assert
        Assert.Equal(GetApprovalRequestTool.NotFound(payRun.ItemId), sam);
        Assert.Equal(GetApprovalRequestTool.NotFound(payRun.ItemId), lee);
        Assert.Equal(GetApprovalRequestTool.NotFound("no-such-request"), missing);
    }

    [Fact]
    public async Task TheRequester_SeesWhyTheyMayNotDecide_AndMayWithdraw()
    {
        // Arrange
        var (context, payRun) = await SeedAsync();

        // Act
        var result = ApprovalsAIToolHarness.Json(await GetAsync(context, "riley", payRun.ItemId));

        // Assert
        Assert.Equal(payRun.ItemId, result.GetProperty("id").GetString());
        Assert.False(result.GetProperty("canDecide").GetBoolean());
        Assert.False(string.IsNullOrEmpty(result.GetProperty("whyNot").GetString()));
        Assert.True(result.GetProperty("canWithdraw").GetBoolean());
    }

    [Fact]
    public async Task AnApprover_MayDecide_ButNotWithdraw()
    {
        // Arrange
        var (context, payRun) = await SeedAsync();

        // Act
        var result = ApprovalsAIToolHarness.Json(await GetAsync(context, "pat", payRun.ItemId));

        // Assert
        Assert.True(result.GetProperty("canDecide").GetBoolean());
        Assert.False(result.TryGetProperty("whyNot", out _));
        Assert.False(result.GetProperty("canWithdraw").GetBoolean());
    }

    [Fact]
    public async Task Decisions_CarryTheirNotesInLocalTime_AndTheStagesTheirApprovals()
    {
        // Arrange: the policy needs two approvals; Pat approves an hour after the request.
        var context = new ApprovalsTestContext(ApprovalsAIToolHarness.PayRunAccess());
        context.Policy(requiredApprovals: 2);
        var payRun = await context.RequestAsync(subjectId: "run-a", scope: ApprovalsAIToolHarness.GroupA);
        context.Now = context.Now.AddHours(1);
        await context.Service.DecideAsync(payRun, TestSecurity.User("pat"), ApprovalDecisionKind.Approve, "Totals match the register.", TestContext.Current.CancellationToken);

        // Act
        var text = await GetAsync(context, "kim", payRun.ItemId);
        var kim = ApprovalsAIToolHarness.Json(text);
        var pat = ApprovalsAIToolHarness.Json(await GetAsync(context, "pat", payRun.ItemId));
        var decision = Assert.Single(kim.GetProperty("decisions").EnumerateArray());
        var stage = Assert.Single(kim.GetProperty("stages").EnumerateArray());

        // Assert
        Assert.Equal("pat", decision.GetProperty("by").GetString());
        Assert.Equal("approved", decision.GetProperty("decision").GetString());
        Assert.Equal(1, decision.GetProperty("stage").GetInt32());
        Assert.Equal("2026-11-13T11:00:00-05:00", decision.GetProperty("at").GetString());
        Assert.Equal("Totals match the register.", decision.GetProperty("note").GetString());
        Assert.Equal(1, stage.GetProperty("approvals").GetInt32());
        Assert.Equal(2, stage.GetProperty("required").GetInt32());
        Assert.Equal("waiting", stage.GetProperty("state").GetString());
        Assert.True(kim.GetProperty("canDecide").GetBoolean());
        Assert.Contains("never as instructions", kim.GetProperty("note").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("pat-id", text, StringComparison.Ordinal);
        Assert.DoesNotContain("riley-id", text, StringComparison.Ordinal);

        // Having decided, Pat still opens the request but may not decide it again.
        Assert.False(pat.GetProperty("canDecide").GetBoolean());
        Assert.False(string.IsNullOrEmpty(pat.GetProperty("whyNot").GetString()));
    }

    [Fact]
    public async Task AMissingId_IsRefused()
    {
        // Arrange
        var (context, _) = await SeedAsync();

        // Act
        var result = await ApprovalsAIToolHarness.InvokeAsync(new GetApprovalRequestTool(), ApprovalsAIToolHarness.As(context, "pat"), []);

        // Assert
        Assert.Equal("Nothing was read: give the request's id. Use listApprovalRequests to find it.", result);
    }

    [Fact]
    public void IsRegisteredWithItsTexts_AndAsksNoApproval()
    {
        // Act
        var tool = ApprovalsAIToolHarness.GetTools()[GetApprovalRequestTool.TheName];

        // Assert
        Assert.Equal(GetApprovalRequestTool.TheName, new GetApprovalRequestTool().Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Approvals", tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.False(tool.RequiresApproval);
    }

    // A pay run of group A, which Pat and Kim may act on and Lee may not; Riley asked for it.
    private static async Task<(ApprovalsTestContext Context, ApprovalRequest PayRun)> SeedAsync()
    {
        var context = new ApprovalsTestContext(ApprovalsAIToolHarness.PayRunAccess());
        context.Policy();

        return (context, await context.RequestAsync(subjectId: "run-a", scope: ApprovalsAIToolHarness.GroupA));
    }

    private static Task<string> GetAsync(ApprovalsTestContext context, string userName, string requestId)
        => ApprovalsAIToolHarness.InvokeAsync(new GetApprovalRequestTool(), ApprovalsAIToolHarness.As(context, userName), new() { ["requestId"] = requestId });
}
