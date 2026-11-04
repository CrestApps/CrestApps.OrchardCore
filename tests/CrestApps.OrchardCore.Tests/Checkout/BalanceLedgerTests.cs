using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Gift cards and store credit change their balances through one ledger: never below zero, at the currency's
/// precision, each change recorded as it was made, and stored in the same shape as before.
/// </summary>
public sealed class BalanceLedgerTests
{
    private static readonly DateTime _now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AChange_NeverGoesBelowZero_AndRecordsWhatActuallyChanged()
    {
        var activity = new List<BalanceLedgerEntry>();

        var balance = BalanceLedger.ChangeBalance(30m, -50m, "USD", activity, "Spent", _now, "s1", "ann");

        Assert.Equal(0m, balance);

        var entry = Assert.Single(activity);

        Assert.Equal((-30m, 0m, "Spent", "s1", "ann", _now), (entry.Amount, entry.Balance, entry.Reason, entry.SessionId, entry.Actor, entry.CreatedUtc));
    }

    [Theory]
    [InlineData("JPY", 1000.4, 1000)]
    [InlineData("KWD", 1.2345, 1.235)]
    [InlineData("USD", 10.005, 10.01)]
    public void ABalance_IsKeptAtItsCurrencysPrecision(string currency, double amount, double expected)
    {
        var balance = BalanceLedger.ChangeBalance(0m, (decimal)amount, currency, new List<BalanceLedgerEntry>(), "Issued", _now);

        Assert.Equal((decimal)expected, balance);
    }

    [Fact]
    public void AGiftCardAndStoreCredit_ChangeTheirBalanceThroughTheLedger_AndKeepTheirStoredShape()
    {
        var card = new GiftCard { Balance = 50m, Currency = "USD" };
        var credit = new StoreCreditAccount { Balance = 5m, Currency = "USD" };

        card.ChangeBalance(-20m, "Spent", _now, "s1");
        credit.ChangeBalance(10m, "Refund", _now, actor: "admin");

        Assert.Equal((30m, -20m), (card.Balance, Assert.Single(card.Activity).Amount));
        Assert.Equal((15m, 10m), (credit.Balance, Assert.Single(credit.Activity).Amount));

        // The stored entry keeps exactly the fields it had.
        var stored = JsonSerializer.SerializeToNode(card.Activity[0]).AsObject().Select(property => property.Key).Order();

        Assert.Equal(["Actor", "Amount", "Balance", "CreatedUtc", "Reason", "SessionId"], stored);

        var read = JsonSerializer.Deserialize<StoreCreditActivity>(JsonNode.Parse("""{"CreatedUtc":"2026-11-03T12:00:00Z","Amount":10,"Balance":15,"Reason":"Refund","SessionId":null,"Actor":"admin"}""").ToJsonString());

        Assert.Equal((10m, 15m, "admin"), (read.Amount, read.Balance, read.Actor));
    }
}
