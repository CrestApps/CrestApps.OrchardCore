using CrestApps.OrchardCore.Approvals.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Tests.Doubles;

namespace CrestApps.OrchardCore.Tests.Approvals;

/// <summary>
/// The review of Approvals: a request keeps the rules of the policy as they were when it was asked, a newer version of
/// the subject replaces a pending request, two decisions at once both count, and a policy that needs more approvers
/// than its rules name is found.
/// </summary>
public sealed class ApprovalReviewFixesTests
{
    [Fact]
    public async Task APolicyLoosenedWhileARequestWaits_DoesNotLetTheRequesterApproveIt()
    {
        // Arrange
        var context = new ApprovalsTestContext();
        var policy = context.Policy();
        var request = await context.RequestAsync(requester: "riley");

        // Act: the policy editor loosens it, tries to approve their own request, and tightens it again.
        policy.AllowRequesterToApprove = true;
        policy.Approvers.Add(new ApproverRule { Kind = ApproverKind.Permission, Value = "Everyone" });
        var result = await context.Service.DecideAsync(request, TestSecurity.User("riley"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(ApprovalStatus.Pending, request.Status);
        Assert.False(request.AllowRequesterToApprove);
        Assert.Single(request.Approvers);
    }

    [Fact]
    public async Task ANewVersionOfTheSubject_ReplacesThePendingRequest_AndTheSameVersionReusesIt()
    {
        // Arrange
        var context = new ApprovalsTestContext();
        var handler = new RecordingSubjectHandler();
        context.Handlers.Add(handler);
        context.Policy();

        // Act
        var first = await Submit(context, "v1");
        var same = await Submit(context, "v1");
        var second = await Submit(context, "v2");

        // Assert
        Assert.Same(first, same);
        Assert.NotSame(first, second);
        Assert.Equal(ApprovalStatus.Canceled, first.Status);
        Assert.Contains("changed", first.CancelReason, StringComparison.Ordinal);
        Assert.Equal("v2", second.SubjectVersion);
        Assert.Equal(ApprovalStatus.Pending, second.Status);
        Assert.Equal([ApprovalStatus.Canceled], handler.Closed);
    }

    [Fact]
    public async Task TwoApproversAtOnce_BothCount()
    {
        // Arrange
        var context = new ApprovalsTestContext();
        context.Policy(requiredApprovals: 2);
        var request = await context.RequestAsync();

        // Act
        var results = await Task.WhenAll(
            Task.Run(() => context.Service.DecideAsync(request, TestSecurity.User("pat"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken)),
            Task.Run(() => context.Service.DecideAsync(request, TestSecurity.User("lee"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken)));

        // Assert
        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(2, request.Decisions.Count);
        Assert.Equal(ApprovalStatus.Approved, request.Status);
        Assert.Equal(2, context.Locks.Taken["Approvals:Request:" + request.ItemId]);
    }

    [Fact]
    public async Task CountPeople_FindsAPolicyThatNeedsMoreApproversThanItsRulesName()
    {
        // Arrange
        var context = new ApprovalsTestContext();
        context.Directory.People["Small"] = ["pat"];
        var small = new ApprovalPolicy { Name = "Small", RequiredApprovals = 2, Approvers = [new ApproverRule { Kind = ApproverKind.Permission, Value = "Small" }] };
        var manager = new ApprovalPolicy { Name = "Manager", RequiredApprovals = 2, Approvers = [new ApproverRule { Kind = ApproverKind.Manager }] };

        // Act
        var smallCount = await context.Directory.CountPeopleAsync(small, TestContext.Current.CancellationToken);
        var managerCount = await context.Directory.CountPeopleAsync(manager, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, smallCount);
        Assert.Null(managerCount);
    }

    private static async Task<ApprovalRequest> Submit(ApprovalsTestContext context, string version)
    {
        var result = await context.Service.RequestAsync(new ApprovalSubmission
        {
            Policy = "PayRunApproval",
            SubjectKind = "PayRun",
            SubjectId = "run-1",
            Title = "Approve the run",
            SubjectVersion = version,
        }, TestSecurity.User("riley"), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));

        return result.Request;
    }
}
