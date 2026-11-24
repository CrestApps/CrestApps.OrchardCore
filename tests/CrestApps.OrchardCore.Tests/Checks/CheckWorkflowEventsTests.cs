using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Workflows;

namespace CrestApps.OrchardCore.Tests.Checks;

/// <summary>
/// The Checks workflow events: each change starts exactly one event, so a stopped or reissued check starts its own
/// event and not Check Voided.
/// </summary>
public sealed class CheckWorkflowEventsTests
{
    [Theory]
    [InlineData(CheckChangeKind.Issued, new[] { nameof(CheckIssuedEvent) })]
    [InlineData(CheckChangeKind.Reprinted, new[] { nameof(CheckPrintedEvent) })]
    [InlineData(CheckChangeKind.Voided, new[] { nameof(CheckVoidedEvent) })]
    [InlineData(CheckChangeKind.StopPayment, new[] { nameof(CheckStopPaymentEvent) })]
    [InlineData(CheckChangeKind.Reissued, new[] { nameof(CheckReissuedEvent) })]
    [InlineData(CheckChangeKind.Stale, new[] { nameof(CheckStaleEvent) })]
    [InlineData(CheckChangeKind.Uncleared, new[] { nameof(CheckUnclearedEvent) })]
    [InlineData(CheckChangeKind.Spoiled, new[] { nameof(CheckVoidedEvent) })]
    [InlineData(CheckChangeKind.Updated, new[] { nameof(CheckUpdatedEvent) })]
    public void EachCheckChange_StartsItsEvents(CheckChangeKind kind, string[] expected)
        => Assert.Equal(expected, ChecksWorkflowObserver.EventsFor(kind));

    [Fact]
    public void EveryCheckChange_StartsAtMostOneEvent_AndOnlyAVoidOrASpoiledSheetStartsCheckVoided()
    {
        foreach (var kind in Enum.GetValues<CheckChangeKind>())
        {
            var events = ChecksWorkflowObserver.EventsFor(kind);

            Assert.True(events.Length <= 1, $"{kind} starts {string.Join(", ", events)}; a change starts one event.");
            Assert.Equal(kind is CheckChangeKind.Voided or CheckChangeKind.Spoiled, events.Contains(nameof(CheckVoidedEvent)));
        }
    }

    [Fact]
    public void AVoidedChecksInput_CarriesTheVoidDate_AndAnUnclearedOneTheDateItHadCleared()
    {
        var check = new CheckRecord { ItemId = "c1", Number = 1001, CheckDate = new DateOnly(2026, 11, 20), Status = CheckStatus.Voided };

        var voided = ChecksWorkflowObserver.Input(new CheckChange { Check = check, Kind = CheckChangeKind.Voided, VoidedOn = new DateOnly(2026, 11, 25) });
        var uncleared = ChecksWorkflowObserver.Input(new CheckChange { Check = check, Kind = CheckChangeKind.Uncleared, ClearedOn = new DateOnly(2026, 11, 27) });

        Assert.Equal(("2026-11-25", null), (voided["VoidedOn"], voided["ClearedOn"]));
        Assert.Equal("2026-11-27", uncleared["ClearedOn"]);
    }

    [Fact]
    public void AReissuedChecksInput_NamesTheNewCheck()
    {
        var check = new CheckRecord { ItemId = "c1", Number = 1001, BankAccountId = "a1", PayeeName = "Lee", Amount = 400m, Currency = "USD", CheckDate = new DateOnly(2026, 11, 20), Status = CheckStatus.Voided };

        var input = ChecksWorkflowObserver.Input(new CheckChange { Check = check, Kind = CheckChangeKind.Reissued, ReplacedBy = new CheckRecord { ItemId = "c2" }, Reason = "Lost", Actor = "pat" });

        Assert.Equal(("c1", 1001L, 400m, "2026-11-20", "Voided"), (input["CheckId"], input["Number"], input["Amount"], input["CheckDate"], input["Status"]));
        Assert.Equal(("Reissued", "c2", "Lost", "pat"), (input["Change"], input["ReplacedById"], input["Reason"], input["Actor"]));
    }
}
