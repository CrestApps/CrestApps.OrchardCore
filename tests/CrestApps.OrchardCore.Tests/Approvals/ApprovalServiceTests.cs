using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Tests.Doubles;

namespace CrestApps.OrchardCore.Tests.Approvals;

/// <summary>
/// The approval rules: who may decide, how many must, the two built-in rules, notes, reuse of a pending request,
/// withdrawing, and what subjects and observers are told.
/// </summary>
public sealed class ApprovalServiceTests
{
    [Fact]
    public async Task RequestAsync_SetsThePolicyNeedsAndDueTime_AndTellsObservers()
    {
        // Arrange
        var context = new ApprovalsTestContext();
        context.Policy(requiredApprovals: 2, dueAfterHours: 48);

        // Act
        var request = await context.RequestAsync();

        // Assert
        Assert.Equal(ApprovalStatus.Pending, request.Status);
        Assert.Equal(2, request.RequiredApprovals);
        Assert.Equal("Pay run approval", request.PolicyTitle);
        Assert.Equal("riley-id", request.RequesterUserId);
        Assert.Equal(context.Now.AddHours(48), request.DueUtc);
        Assert.Equal(ApprovalChangeKind.Requested, Assert.Single(context.Observer.Changes).Kind);
    }

    [Fact]
    public async Task RequestAsync_WhileARequestAboutTheSubjectIsPending_GivesThatRequest()
    {
        // Arrange
        var context = new ApprovalsTestContext();
        context.Policy();
        var first = await context.RequestAsync();

        // Act
        var second = await context.RequestAsync(requester: "kim");

        // Assert
        Assert.Same(first, second);
        Assert.Single(context.Store.Entries);
        Assert.Single(context.Observer.Changes);
    }

    [Fact]
    public async Task RequestAsync_WithoutAPolicyOrSubject_Fails()
    {
        // Arrange
        var context = new ApprovalsTestContext();

        // Act
        var result = await context.Service.RequestAsync(new ApprovalSubmission { Policy = "Missing", Title = "x" }, TestSecurity.User("riley"), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(2, result.Errors.Count);
        Assert.Empty(context.Store.Entries);
    }

    [Fact]
    public async Task Eligibility_TheRequester_MayNotApprove_UnlessThePolicyAllows()
    {
        // Arrange
        var context = new ApprovalsTestContext();
        var policy = context.Policy();
        var request = await context.RequestAsync(requester: "riley");

        // Act
        var refused = await context.Service.GetEligibilityAsync(request, TestSecurity.User("riley"), TestContext.Current.CancellationToken);
        policy.AllowRequesterToApprove = true;
        var allowed = await context.Service.GetEligibilityAsync(request, TestSecurity.User("riley"), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(refused.CanDecide);
        Assert.Contains("asked", refused.Reason, StringComparison.Ordinal);
        Assert.True(allowed.CanDecide);
    }

    [Fact]
    public async Task Eligibility_ThePersonTheRequestIsAbout_MayNotDecide_UnlessThePolicyAllows()
    {
        // Arrange
        var context = new ApprovalsTestContext();
        var policy = context.Policy();
        var request = await context.RequestAsync(subjectUserIds: ["pat-id"]);

        // Act
        var refused = await context.Service.GetEligibilityAsync(request, TestSecurity.User("pat"), TestContext.Current.CancellationToken);
        policy.AllowSubjectToDecide = true;
        var allowed = await context.Service.GetEligibilityAsync(request, TestSecurity.User("pat"), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(refused.CanDecide);
        Assert.Contains("about you", refused.Reason, StringComparison.Ordinal);
        Assert.True(allowed.CanDecide);
    }

    [Fact]
    public async Task Eligibility_SomeoneNoRuleNames_OrNobodySignedIn_MayNotDecide()
    {
        // Arrange
        var context = new ApprovalsTestContext();
        context.Policy();
        var request = await context.RequestAsync();

        // Act
        var stranger = await context.Service.GetEligibilityAsync(request, TestSecurity.User("sam"), TestContext.Current.CancellationToken);
        var nobody = await context.Service.GetEligibilityAsync(request, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(stranger.CanDecide);
        Assert.False(nobody.CanDecide);
    }

    [Fact]
    public async Task DecideAsync_TwoApprovalsNeeded_StaysPendingAfterOne_AndClosesTheSubjectAfterTwo()
    {
        // Arrange
        var context = new ApprovalsTestContext();
        var handler = new RecordingSubjectHandler();
        context.Handlers.Add(handler);
        context.Policy(requiredApprovals: 2);
        var request = await context.RequestAsync();

        // Act
        var first = await context.Service.DecideAsync(request, TestSecurity.User("pat"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken);
        var again = await context.Service.DecideAsync(request, TestSecurity.User("pat"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken);
        var pendingAfterOne = request.Status;
        var second = await context.Service.DecideAsync(request, TestSecurity.User("lee"), ApprovalDecisionKind.Approve, "Looks right", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.Succeeded);
        Assert.False(again.Succeeded);
        Assert.Equal(ApprovalStatus.Pending, pendingAfterOne);
        Assert.True(second.Succeeded);
        Assert.Equal(ApprovalStatus.Approved, request.Status);
        Assert.Equal(context.Now, request.ClosedUtc);
        Assert.Equal([ApprovalStatus.Approved], handler.Closed);
        Assert.Equal(
            [ApprovalChangeKind.Requested, ApprovalChangeKind.PartlyApproved, ApprovalChangeKind.Approved],
            context.Observer.Changes.Select(change => change.Kind));
    }

    [Fact]
    public async Task DecideAsync_Reject_NeedsANote_AndRejectsAtOnce()
    {
        // Arrange
        var context = new ApprovalsTestContext();
        context.Policy(requiredApprovals: 2);
        var request = await context.RequestAsync();

        // Act
        var withoutNote = await context.Service.DecideAsync(request, TestSecurity.User("pat"), ApprovalDecisionKind.Reject, " ", TestContext.Current.CancellationToken);
        var withNote = await context.Service.DecideAsync(request, TestSecurity.User("pat"), ApprovalDecisionKind.Reject, "Overtime is missing", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(withoutNote.Succeeded);
        Assert.True(withNote.Succeeded);
        Assert.Equal(ApprovalStatus.Rejected, request.Status);
        Assert.Equal("Overtime is missing", Assert.Single(request.Decisions).Note);
        Assert.Equal("Overtime is missing", context.Observer.Changes.Last().Decision.Note);
    }

    [Fact]
    public async Task CancelAsync_WithdrawsAPendingRequest_ButNotADecidedOne()
    {
        // Arrange
        var context = new ApprovalsTestContext();
        var handler = new RecordingSubjectHandler();
        context.Handlers.Add(handler);
        context.Policy();
        var pending = await context.RequestAsync(subjectId: "run-1");
        var decided = await context.RequestAsync(subjectId: "run-2");
        await context.Service.DecideAsync(decided, TestSecurity.User("pat"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken);

        // Act
        var canceled = await context.Service.CancelAsync(pending, "Wrong period", TestContext.Current.CancellationToken);
        var refused = await context.Service.CancelAsync(decided, "Too late", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(canceled);
        Assert.False(refused);
        Assert.Equal(ApprovalStatus.Canceled, pending.Status);
        Assert.Equal("Wrong period", pending.CancelReason);
        Assert.Equal(ApprovalStatus.Approved, decided.Status);
        Assert.Equal([ApprovalStatus.Approved, ApprovalStatus.Canceled], handler.Closed);
    }

    [Fact]
    public async Task DecideAsync_OnImportedHistory_IsRefused()
    {
        // Arrange
        var context = new ApprovalsTestContext();
        context.Policy();
        var request = await context.RequestAsync();
        request.IsHistorical = true;

        // Act
        var result = await context.Service.DecideAsync(request, TestSecurity.User("pat"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(await context.Service.CancelAsync(request, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetApproverUserIdsAsync_LeavesOutTheRequester_TheSubject_AndThoseWhoDecided()
    {
        // Arrange
        var context = new ApprovalsTestContext();
        context.Policy(requiredApprovals: 2);
        var request = await context.RequestAsync(requester: "riley", subjectUserIds: ["kim-id"]);
        await context.Service.DecideAsync(request, TestSecurity.User("pat"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken);

        // Act
        var approvers = await context.Service.GetApproverUserIdsAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["lee-id"], approvers);
    }
}
