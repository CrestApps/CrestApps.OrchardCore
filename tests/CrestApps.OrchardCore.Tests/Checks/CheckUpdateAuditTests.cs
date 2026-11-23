using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Banking.Checks.AuditTrail;
using CrestApps.OrchardCore.Banking.Checks.Models;

namespace CrestApps.OrchardCore.Tests.Checks;

/// <summary>
/// What the Audit Trail keeps of an edit of a check waiting to print.
/// </summary>
public sealed class CheckUpdateAuditTests
{
    [Fact]
    public void Data_KeepsWhatTheCheckWasAndBecame_AndWhetherTheAddressChanged()
    {
        // Arrange
        var previous = new CheckDetails { PayeeName = "Pat Doe", Amount = 100m, CheckDate = new DateOnly(2026, 11, 13), Memo = "Old" };
        var check = new CheckRecord
        {
            ItemId = "c1",
            BankAccountId = "bank1",
            Number = 1001,
            Currency = "USD",
            PayeeName = "Lee Roe",
            PayeeAddress = new Address { AddressLine1 = "1 Main St", City = "Detroit", Country = "US" },
            Amount = 150m,
            CheckDate = new DateOnly(2026, 11, 20),
            Memo = "New",
        };

        // Act
        var data = AuditTrailCheckObserver.Data(check, previous, "Wrong payee");

        // Assert
        Assert.Equal(("Pat Doe", "Lee Roe"), (data.PreviousPayeeName, data.PayeeName));
        Assert.Equal((100m, 150m), (data.PreviousAmount, data.Amount));
        Assert.Equal(("2026-11-13", "2026-11-20"), (data.PreviousCheckDate, data.CheckDate));
        Assert.Equal(("Old", "New"), (data.PreviousMemo, data.Memo));
        Assert.True(data.AddressChanged);
        Assert.Equal("Wrong payee", data.Reason);
    }
}
