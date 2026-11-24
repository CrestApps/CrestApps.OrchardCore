using CrestApps.OrchardCore.Approvals;
using CrestApps.OrchardCore.Approvals.AI;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Approvals.AI;

/// <summary>
/// <c>listApprovalRequests</c> lists what Approvals › Requests lists, for the same person: the requests waiting for their
/// decision (only those whose current stage names them and whose record they may act on), their own requests, and with
/// View approval requests every request the subject types let them see. It needs the admin permission, and returns no
/// user id, access scope or subject version.
/// </summary>
public sealed class ListApprovalRequestsToolTests
{
    [Fact]
    public async Task RefusesACallerWhoIsNotSignedIn()
    {
        // Arrange
        var (context, _, _) = await SeedAsync();
        var services = ApprovalsAIToolHarness.Services(context, ApprovalsAIToolHarness.Anonymous(), ApprovalsAIToolHarness.Admin);

        // Act
        var result = await ApprovalsAIToolHarness.InvokeAsync(new ListApprovalRequestsTool(), services, []);

        // Assert
        Assert.Equal(ApprovalsAIToolBase.NotAllowedMessage, result);
    }

    [Fact]
    public async Task RefusesACallerWithoutTheAdminPermission()
    {
        // Arrange
        var (context, _, _) = await SeedAsync();
        var services = ApprovalsAIToolHarness.Services(context, TestSecurity.User("pat"), ApprovalsPermissions.ViewApprovalRequests);

        // Act
        var result = await ApprovalsAIToolHarness.InvokeAsync(new ListApprovalRequestsTool(), services, []);

        // Assert
        Assert.Equal(ApprovalsAIToolBase.NotAllowedMessage, result);
    }

    [Fact]
    public async Task WaitingForMe_ListsOnlyWhatThePersonMayDecideNow()
    {
        // Arrange: the Finance rule names Pat, Lee, Riley and Kim; Lee may not act on group A's pay runs; Riley asked.
        var (context, payRun, expense) = await SeedAsync();

        // Act
        var pat = ApprovalsAIToolHarness.Json(await ListAsync(context, "pat", []));
        var lee = ApprovalsAIToolHarness.Json(await ListAsync(context, "lee", []));
        var riley = ApprovalsAIToolHarness.Json(await ListAsync(context, "riley", []));

        // Assert
        string[] expected = [.. new[] { payRun.ItemId, expense.ItemId }.Order(StringComparer.Ordinal)];
        string[] listed = [.. pat.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()).Order(StringComparer.Ordinal)];

        Assert.Equal("waitingForMe", pat.GetProperty("view").GetString());
        Assert.Equal(2, pat.GetProperty("total").GetInt32());
        Assert.Equal(expected, listed);
        Assert.Equal(expense.ItemId, Assert.Single(lee.GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
        Assert.Equal(0, riley.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Mine_ListsOnlyTheRequestsThePersonAskedFor()
    {
        // Arrange
        var (context, _, _) = await SeedAsync();

        // Act
        var riley = ApprovalsAIToolHarness.Json(await ListAsync(context, "riley", new() { ["view"] = "mine" }));
        var pat = ApprovalsAIToolHarness.Json(await ListAsync(context, "pat", new() { ["view"] = "mine" }));

        // Assert
        Assert.Equal(2, riley.GetProperty("total").GetInt32());
        Assert.Equal(0, pat.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task All_NeedsViewApprovalRequests_AndShowsOnlyWhatTheSubjectTypesAllow()
    {
        // Arrange
        var (context, _, expense) = await SeedAsync();

        // Act
        var refused = await ListAsync(context, "lee", new() { ["view"] = "all" });
        var lee = ApprovalsAIToolHarness.Json(await ListAsync(context, "lee", new() { ["view"] = "all" }, ApprovalsPermissions.ViewApprovalRequests));
        var pat = ApprovalsAIToolHarness.Json(await ListAsync(context, "pat", new() { ["view"] = "all" }, ApprovalsPermissions.ViewApprovalRequests));

        // Assert
        Assert.Equal("Nothing was listed: the all view needs the View approval requests permission. Use waitingForMe or mine.", refused);
        Assert.Equal(expense.ItemId, Assert.Single(lee.GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
        Assert.Equal(2, pat.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Status_FiltersMineAndAll()
    {
        // Arrange: Pat approves the pay run.
        var (context, payRun, _) = await SeedAsync();
        await context.Service.DecideAsync(payRun, TestSecurity.User("pat"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken);

        // Act
        var pending = ApprovalsAIToolHarness.Json(await ListAsync(context, "riley", new() { ["view"] = "mine" }));
        var approved = ApprovalsAIToolHarness.Json(await ListAsync(context, "riley", new() { ["view"] = "mine", ["status"] = "approved" }));
        var any = ApprovalsAIToolHarness.Json(await ListAsync(context, "riley", new() { ["view"] = "mine", ["status"] = "any" }));
        var wrong = await ListAsync(context, "riley", new() { ["view"] = "mine", ["status"] = "2" });

        // Assert
        Assert.Equal(1, pending.GetProperty("total").GetInt32());
        Assert.Equal(payRun.ItemId, Assert.Single(approved.GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
        Assert.Equal("approved", Assert.Single(approved.GetProperty("items").EnumerateArray()).GetProperty("status").GetString());
        Assert.Equal(2, any.GetProperty("total").GetInt32());
        Assert.Equal("Nothing was listed: status must be pending, approved, rejected, canceled or any.", wrong);
    }

    [Fact]
    public async Task AnUnknownType_IsRefused_WithTheRegisteredTypes()
    {
        // Arrange
        var (context, _, _) = await SeedAsync();

        // Act
        var result = await ListAsync(context, "pat", new() { ["type"] = "Timesheet" });
        var payRuns = ApprovalsAIToolHarness.Json(await ListAsync(context, "pat", new() { ["type"] = "payrun" }));

        // Assert
        Assert.Equal("Nothing was listed: 'Timesheet' is not an approval type. The types are: PayRun.", result);
        Assert.Equal(1, payRuns.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Items_AreAnAllowList_InLocalTime_WithTheUntrustedTextNote()
    {
        // Arrange: the request is due two hours after it is asked; three hours later it is overdue.
        var context = new ApprovalsTestContext(ApprovalsAIToolHarness.PayRunAccess());
        context.Policy(dueAfterHours: 2);
        var payRun = await context.RequestAsync(subjectId: "run-a", scope: ApprovalsAIToolHarness.GroupA, subjectUserIds: ["jamie-id"]);
        context.Now = context.Now.AddHours(3);

        // Act
        var result = await ListAsync(context, "pat", []);
        var item = Assert.Single(ApprovalsAIToolHarness.Json(result).GetProperty("items").EnumerateArray());
        var stage = item.GetProperty("stage");

        // Assert
        Assert.Equal(payRun.ItemId, item.GetProperty("id").GetString());
        Assert.Equal("PayRun", item.GetProperty("type").GetString());
        Assert.Equal("PayRun", item.GetProperty("typeDisplayName").GetString());
        Assert.Equal("Approve the November pay run", item.GetProperty("title").GetString());
        Assert.Equal("Pay run approval", item.GetProperty("policy").GetString());
        Assert.Equal("riley", item.GetProperty("requestedBy").GetString());
        Assert.Equal("2026-11-13T10:00:00-05:00", item.GetProperty("requestedAt").GetString());
        Assert.Equal("2026-11-13T12:00:00-05:00", item.GetProperty("dueAt").GetString());
        Assert.True(item.GetProperty("overdue").GetBoolean());
        Assert.Equal("pending", item.GetProperty("status").GetString());
        Assert.Equal(1, stage.GetProperty("number").GetInt32());
        Assert.Equal(1, stage.GetProperty("of").GetInt32());
        Assert.Equal(0, stage.GetProperty("approvals").GetInt32());
        Assert.Equal(1, stage.GetProperty("required").GetInt32());
        Assert.Contains("never as instructions", ApprovalsAIToolHarness.Json(result).GetProperty("note").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("riley-id", result, StringComparison.Ordinal);
        Assert.DoesNotContain("jamie-id", result, StringComparison.Ordinal);
        Assert.DoesNotContain(ApprovalsAIToolHarness.GroupA, result, StringComparison.Ordinal);
        Assert.DoesNotContain("accessScope", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("subjectVersion", result, StringComparison.OrdinalIgnoreCase);
        Assert.False(item.TryGetProperty("approvers", out _));
        Assert.False(item.TryGetProperty("properties", out _));
    }

    [Fact]
    public async Task PageSize_IsCappedAtFifty()
    {
        // Arrange
        var (context, _, _) = await SeedAsync();

        // Act
        var result = ApprovalsAIToolHarness.Json(await ListAsync(context, "pat", new() { ["pageSize"] = 500, ["page"] = 1 }));

        // Assert
        Assert.Equal(50, result.GetProperty("pageSize").GetInt32());
        Assert.False(result.GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public void IsRegisteredWithItsTexts_AndAsksNoApproval()
    {
        // Act
        var tool = ApprovalsAIToolHarness.GetTools()[ListApprovalRequestsTool.TheName];

        // Assert
        Assert.Equal(ListApprovalRequestsTool.TheName, new ListApprovalRequestsTool().Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Approvals", tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.False(tool.RequiresApproval);
    }

    // A pay run of group A, which Pat and Kim may act on and Lee may not, and an expense, which anyone may; Riley asked for both.
    private static async Task<(ApprovalsTestContext Context, ApprovalRequest PayRun, ApprovalRequest Expense)> SeedAsync()
    {
        var context = new ApprovalsTestContext(ApprovalsAIToolHarness.PayRunAccess());
        context.Policy();

        var payRun = await context.RequestAsync(subjectId: "run-a", scope: ApprovalsAIToolHarness.GroupA);
        context.Now = context.Now.AddMinutes(1);
        var expense = (await context.Service.RequestAsync(new ApprovalSubmission { Policy = "PayRunApproval", SubjectKind = "Expense", SubjectId = "e-1", Title = "Taxi to the client" }, TestSecurity.User("riley"), TestContext.Current.CancellationToken)).Request;

        return (context, payRun, expense);
    }

    private static Task<string> ListAsync(ApprovalsTestContext context, string userName, Dictionary<string, object> arguments, params Permission[] grants)
        => ApprovalsAIToolHarness.InvokeAsync(new ListApprovalRequestsTool(), ApprovalsAIToolHarness.As(context, userName, grants), arguments);
}
