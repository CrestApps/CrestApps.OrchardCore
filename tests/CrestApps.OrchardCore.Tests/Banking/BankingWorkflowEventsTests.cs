using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Workflows;
using CrestApps.OrchardCore.Tests.Doubles;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Tests.Banking;

/// <summary>
/// The Banking workflow events: a status change starts Bank Account Status Changed besides Bank Account Changed, with
/// the new and previous status, and the event can start for one status only.
/// </summary>
public sealed class BankingWorkflowEventsTests
{
    [Fact]
    public async Task VerifyingAndClosingAnAccount_StartBankAccountStatusChanged_ButAddingItDoesNot()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync();

        var changes = context.Observer.Changes;
        var created = changes[^1];

        // Act: the input is read when each change is told, since the account object moves on.
        Assert.True(await context.Service.ChangeStatusAsync(account, BankAccountStatus.Verified, "Called the bank"));
        var verified = changes[^1];
        var verifiedEvents = BankingWorkflowObserver.EventsFor(verified);
        var input = BankingWorkflowObserver.Input(verified);

        Assert.True(await context.Service.ChangeStatusAsync(account, BankAccountStatus.Closed, "Moved banks"));
        var closed = BankingWorkflowObserver.Input(changes[^1]);

        // Assert
        Assert.Equal([BankAccountChangedEvent.EventName], BankingWorkflowObserver.EventsFor(created));
        Assert.Equal([BankAccountChangedEvent.EventName, nameof(BankAccountStatusChangedEvent)], verifiedEvents);
        Assert.Equal(("Verified", "Unverified", "Called the bank"), (input["Status"], input["PreviousStatus"], input["Reason"]));
        Assert.Equal(("Closed", "Verified", "Moved banks"), (closed["Status"], closed["PreviousStatus"], closed["Reason"]));
        Assert.Contains(nameof(BankAccountStatusChangedEvent), BankingWorkflowObserver.EventsFor(changes[^1]));
        Assert.DoesNotContain(input, entry => entry.Value is string text && text.Contains(BankingTestContext.ValidRoutingNumber, StringComparison.Ordinal));
    }

    [Fact]
    public void ARevealStartsNothing()
    {
        var change = new BankAccountChange { Account = new BankAccount(), Kinds = [BankAccountChangeKind.NumberRevealed] };

        Assert.Empty(BankingWorkflowObserver.EventsFor(change));
    }

    [Theory]
    [InlineData(null, "Closed", true)]
    [InlineData("Closed", "Closed", true)]
    [InlineData("closed", "Closed", true)]
    [InlineData("Verified", "Closed", false)]
    public void TheEvent_StartsForItsStatusOnly(string filter, string status, bool starts)
    {
        var activity = new BankAccountStatusChangedEvent(RecipeRoundTrip.Localizer<BankAccountStatusChangedEvent>()) { Status = filter };
        var context = new WorkflowExecutionContext(new WorkflowType(), new Workflow(), new Dictionary<string, object> { ["Status"] = status }, new Dictionary<string, object>(), new Dictionary<string, object>(), [], null, []);

        Assert.Equal(starts, activity.CanExecute(context, null));
    }
}
