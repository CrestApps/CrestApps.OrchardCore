using CrestApps.OrchardCore.Approvals;
using CrestApps.OrchardCore.Approvals.AI;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Tests.Doubles;

namespace CrestApps.OrchardCore.Tests.Approvals.AI;

/// <summary>
/// <c>decideApprovalRequest</c> decides as the Approve and Reject buttons do: only for a request that opens for the person
/// at the Manage level, and then only as the approval rules allow (the current stage names them, they may act on the
/// record, they did not ask for it, a rejection has a note). The subject's handler is told, and the tool asks for
/// approval because a decision cannot be undone.
/// </summary>
public sealed class DecideApprovalRequestToolTests
{
    [Fact]
    public async Task RefusesACallerWhoIsNotSignedIn_OrWithoutTheAdminPermission()
    {
        // Arrange
        var (context, payRun, _) = await SeedAsync();
        var arguments = new Dictionary<string, object> { ["requestId"] = payRun.ItemId, ["decision"] = "approve" };

        // Act
        var anonymous = await ApprovalsAIToolHarness.InvokeAsync(new DecideApprovalRequestTool(), ApprovalsAIToolHarness.Services(context, ApprovalsAIToolHarness.Anonymous(), ApprovalsAIToolHarness.Admin), arguments);
        var noAdmin = await ApprovalsAIToolHarness.InvokeAsync(new DecideApprovalRequestTool(), ApprovalsAIToolHarness.Services(context, TestSecurity.User("pat"), ApprovalsPermissions.ManageApprovalRequests), arguments);

        // Assert
        Assert.Equal(ApprovalsAIToolBase.NotAllowedMessage, anonymous);
        Assert.Equal(ApprovalsAIToolBase.NotAllowedMessage, noAdmin);
        Assert.Equal(ApprovalStatus.Pending, (await FindAsync(context, payRun)).Status);
    }

    [Fact]
    public async Task ARequestThePersonMayNotActOn_IsAnsweredAsOneThatDoesNotExist_AndStaysPending()
    {
        // Arrange: no rule names Sam; the rule names Lee, but Lee may not act on group A's pay runs.
        var (context, payRun, _) = await SeedAsync();
        var expected = $"Nothing was decided: no approval request has the id '{payRun.ItemId}' that you can act on. Use listApprovalRequests to find it.";

        // Act
        var sam = await DecideAsync(context, "sam", payRun.ItemId, "approve");
        var lee = await DecideAsync(context, "lee", payRun.ItemId, "approve");

        // Assert
        Assert.Equal(expected, sam);
        Assert.Equal(expected, lee);
        Assert.Equal(ApprovalStatus.Pending, (await FindAsync(context, payRun)).Status);
        Assert.Empty((await FindAsync(context, payRun)).Decisions);
    }

    [Fact]
    public async Task AnApprover_Approves_AndTheSubjectsHandlerIsTold()
    {
        // Arrange
        var (context, payRun, subject) = await SeedAsync();

        // Act
        var result = ApprovalsAIToolHarness.Json(await DecideAsync(context, "pat", payRun.ItemId, "approve", "Totals match."));

        // Assert
        Assert.Equal(payRun.ItemId, result.GetProperty("requestId").GetString());
        Assert.Equal("approved", result.GetProperty("decision").GetString());
        Assert.Equal("approved", result.GetProperty("status").GetString());
        Assert.Equal("Approved. The request has the approvals it needs.", result.GetProperty("message").GetString());
        Assert.Equal(ApprovalStatus.Approved, (await FindAsync(context, payRun)).Status);
        Assert.Equal("Totals match.", Assert.Single((await FindAsync(context, payRun)).Decisions).Note);
        Assert.Equal(ApprovalStatus.Approved, Assert.Single(subject.Closed));
    }

    [Fact]
    public async Task TheFirstOfTwoApprovals_IsRecorded_AndSaysHowManyMoreAreNeeded()
    {
        // Arrange
        var (context, payRun, subject) = await SeedAsync(requiredApprovals: 2);

        // Act
        var result = ApprovalsAIToolHarness.Json(await DecideAsync(context, "pat", payRun.ItemId, "approve"));

        // Assert
        Assert.Equal("pending", result.GetProperty("status").GetString());
        Assert.Equal("Your approval is recorded. It needs 1 more.", result.GetProperty("message").GetString());
        Assert.Equal(1, result.GetProperty("stage").GetProperty("approvals").GetInt32());
        Assert.Equal(2, result.GetProperty("stage").GetProperty("required").GetInt32());
        Assert.Empty(subject.Closed);
    }

    [Fact]
    public async Task ARejectionWithoutANote_IsRefused_AndNothingIsRecorded()
    {
        // Arrange
        var (context, payRun, subject) = await SeedAsync();

        // Act
        var result = await DecideAsync(context, "pat", payRun.ItemId, "reject");

        // Assert
        Assert.Equal("Nothing was decided: Say why you reject it.", result);
        Assert.Equal(ApprovalStatus.Pending, (await FindAsync(context, payRun)).Status);
        Assert.Empty((await FindAsync(context, payRun)).Decisions);
        Assert.Empty(subject.Closed);
    }

    [Fact]
    public async Task ARejectionWithANote_Rejects()
    {
        // Arrange
        var (context, payRun, subject) = await SeedAsync();

        // Act
        var result = ApprovalsAIToolHarness.Json(await DecideAsync(context, "pat", payRun.ItemId, "reject", "The overtime is missing."));

        // Assert
        Assert.Equal("rejected", result.GetProperty("status").GetString());
        Assert.Equal("Rejected. The requester is told why.", result.GetProperty("message").GetString());
        Assert.Equal("The overtime is missing.", Assert.Single((await FindAsync(context, payRun)).Decisions).Note);
        Assert.Equal(ApprovalStatus.Rejected, Assert.Single(subject.Closed));
    }

    [Fact]
    public async Task TheRequester_IsRefusedWithTheRulesReason()
    {
        // Arrange: Riley asked, and the policy does not let the requester approve.
        var (context, payRun, _) = await SeedAsync();

        // Act
        var result = await DecideAsync(context, "riley", payRun.ItemId, "approve");

        // Assert
        Assert.Equal("Nothing was decided: You asked for this approval, so you can't decide it.", result);
        Assert.Equal(ApprovalStatus.Pending, (await FindAsync(context, payRun)).Status);
    }

    [Fact]
    public async Task AnUnknownDecision_IsRefused()
    {
        // Arrange
        var (context, payRun, _) = await SeedAsync();

        // Act
        var result = await DecideAsync(context, "pat", payRun.ItemId, "maybe");

        // Assert
        Assert.Equal("Nothing was decided: decision must be approve or reject.", result);
        Assert.Equal(ApprovalStatus.Pending, (await FindAsync(context, payRun)).Status);
    }

    [Fact]
    public void IsRegisteredWithItsTexts_AndAsksForApproval()
    {
        // Act
        var tool = ApprovalsAIToolHarness.GetTools()[DecideApprovalRequestTool.TheName];

        // Assert
        Assert.Equal(DecideApprovalRequestTool.TheName, new DecideApprovalRequestTool().Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Approvals", tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.True(tool.RequiresApproval);
    }

    // A pay run of group A, which Pat and Kim may act on and Lee may not; Riley asked for it. The subject's handler records
    // how its requests close.
    private static async Task<(ApprovalsTestContext Context, ApprovalRequest PayRun, RecordingSubjectHandler Subject)> SeedAsync(int requiredApprovals = 1)
    {
        var context = new ApprovalsTestContext(ApprovalsAIToolHarness.PayRunAccess());
        var subject = new RecordingSubjectHandler();

        context.Handlers.Add(subject);
        context.Policy(requiredApprovals: requiredApprovals);

        return (context, await context.RequestAsync(subjectId: "run-a", scope: ApprovalsAIToolHarness.GroupA), subject);
    }

    private static Task<string> DecideAsync(ApprovalsTestContext context, string userName, string requestId, string decision, string note = null)
    {
        var arguments = new Dictionary<string, object> { ["requestId"] = requestId, ["decision"] = decision };

        if (note is not null)
        {
            arguments["note"] = note;
        }

        return ApprovalsAIToolHarness.InvokeAsync(new DecideApprovalRequestTool(), ApprovalsAIToolHarness.As(context, userName), arguments);
    }

    private static async Task<ApprovalRequest> FindAsync(ApprovalsTestContext context, ApprovalRequest request)
        => await context.Store.FindByIdAsync(request.ItemId, TestContext.Current.CancellationToken);
}
