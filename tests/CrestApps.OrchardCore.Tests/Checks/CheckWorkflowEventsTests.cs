using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Workflows;

namespace CrestApps.OrchardCore.Tests.Checks;

/// <summary>
/// The Checks workflow events: a stale check has its own event, and a stopped or reissued check its own besides Check
/// Voided.
/// </summary>
public sealed class CheckWorkflowEventsTests
{
    [Theory]
    [InlineData(CheckChangeKind.Issued, new[] { nameof(CheckIssuedEvent) })]
    [InlineData(CheckChangeKind.Reprinted, new[] { nameof(CheckPrintedEvent) })]
    [InlineData(CheckChangeKind.Voided, new[] { nameof(CheckVoidedEvent) })]
    [InlineData(CheckChangeKind.StopPayment, new[] { nameof(CheckVoidedEvent), nameof(CheckStopPaymentEvent) })]
    [InlineData(CheckChangeKind.Reissued, new[] { nameof(CheckVoidedEvent), nameof(CheckReissuedEvent) })]
    [InlineData(CheckChangeKind.Stale, new[] { nameof(CheckStaleEvent) })]
    public void EachCheckChange_StartsItsEvents(CheckChangeKind kind, string[] expected)
        => Assert.Equal(expected, ChecksWorkflowObserver.EventsFor(kind));

    [Fact]
    public void AReissuedChecksInput_NamesTheNewCheck()
    {
        var check = new CheckRecord { ItemId = "c1", Number = 1001, BankAccountId = "a1", PayeeName = "Lee", Amount = 400m, Currency = "USD", CheckDate = new DateOnly(2026, 11, 20), Status = CheckStatus.Voided };

        var input = ChecksWorkflowObserver.Input(new CheckChange { Check = check, Kind = CheckChangeKind.Reissued, ReplacedBy = new CheckRecord { ItemId = "c2" }, Reason = "Lost", Actor = "pat" });

        Assert.Equal(("c1", 1001L, 400m, "2026-11-20", "Voided"), (input["CheckId"], input["Number"], input["Amount"], input["CheckDate"], input["Status"]));
        Assert.Equal(("Reissued", "c2", "Lost", "pat"), (input["Change"], input["ReplacedById"], input["Reason"], input["Actor"]));
    }
}
