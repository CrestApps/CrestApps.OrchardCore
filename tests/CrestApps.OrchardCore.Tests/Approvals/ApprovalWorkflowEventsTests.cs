using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Workflows;
using CrestApps.OrchardCore.Tests.Doubles;

namespace CrestApps.OrchardCore.Tests.Approvals;

/// <summary>
/// The Approvals workflow events: a partial approval, a stage approved and a withdrawn request each start their event,
/// and a withdrawn request's input carries its reason.
/// </summary>
public sealed class ApprovalWorkflowEventsTests
{
    [Theory]
    [InlineData(ApprovalChangeKind.Requested, nameof(ApprovalRequestedEvent))]
    [InlineData(ApprovalChangeKind.PartlyApproved, nameof(ApprovalPartlyApprovedEvent))]
    [InlineData(ApprovalChangeKind.StageApproved, nameof(ApprovalStageApprovedEvent))]
    [InlineData(ApprovalChangeKind.Approved, nameof(ApprovalApprovedEvent))]
    [InlineData(ApprovalChangeKind.Rejected, nameof(ApprovalRejectedEvent))]
    [InlineData(ApprovalChangeKind.Canceled, nameof(ApprovalCanceledEvent))]
    public void EachChange_StartsItsEvent(ApprovalChangeKind kind, string expected)
        => Assert.Equal(expected, ApprovalsWorkflowObserver.EventFor(kind));

    [Fact]
    public async Task APartialApprovalAndAWithdrawal_GiveTheWorkflowTheDeciderAndTheReason()
    {
        // Arrange
        var context = new ApprovalsTestContext();
        context.Policy(requiredApprovals: 2);
        var request = await context.RequestAsync();

        // Act
        await context.Service.DecideAsync(request, TestSecurity.User("pat"), ApprovalDecisionKind.Approve, "Fine by me", TestContext.Current.CancellationToken);
        await context.Service.CancelAsync(request, "Wrong period", TestContext.Current.CancellationToken);

        // Assert
        var changes = context.Observer.Changes;

        Assert.Equal([ApprovalChangeKind.Requested, ApprovalChangeKind.PartlyApproved, ApprovalChangeKind.Canceled], changes.Select(change => change.Kind));

        var partly = ApprovalsWorkflowObserver.Input(changes[1], []);
        var canceled = ApprovalsWorkflowObserver.Input(changes[2], []);

        Assert.Equal((request.ItemId, "PartlyApproved", "pat", "Fine by me", 1), (partly["RequestId"], partly["Change"], partly["Decider"], partly["Note"], partly["Stage"]));
        Assert.Equal(("Canceled", "Canceled", "Wrong period"), (canceled["Status"], canceled["Change"], canceled["Note"]));
    }

    [Fact]
    public void AStageApprovedsInput_HasTheNextStageAndItsApprovers()
    {
        var request = new ApprovalRequest { ItemId = "r1", SubjectKind = "PayRun", SubjectId = "run-1", Status = ApprovalStatus.Pending, CurrentStage = 1, Amount = 3200m, Currency = "USD" };

        var input = ApprovalsWorkflowObserver.Input(new ApprovalChange { Request = request, Kind = ApprovalChangeKind.StageApproved, Actor = "pat" }, ["lee", "sam"]);

        Assert.Equal(("PayRun", "run-1", 2, "lee,sam"), (input["SubjectKind"], input["SubjectId"], input["Stage"], input["ApproverUserNames"]));
        Assert.Equal(("StageApproved", 3200m, "USD"), (input["Change"], input["Amount"], input["Currency"]));
    }
}
