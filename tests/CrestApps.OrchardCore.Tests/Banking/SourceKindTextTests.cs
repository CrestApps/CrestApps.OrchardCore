using CrestApps.OrchardCore.Banking.Services;

namespace CrestApps.OrchardCore.Tests.Banking;

/// <summary>
/// Check and disbursement rows showed what they pay as a technical name ("PayStatement"); they show words instead.
/// </summary>
public sealed class SourceKindTextTests
{
    [Theory]
    [InlineData("PayStatement", "Pay statement")]
    [InlineData("PayrollGarnishment", "Payroll garnishment")]
    [InlineData("MarketplacePayout", "Marketplace payout")]
    [InlineData("ACHReturn", "ACH return")]
    [InlineData("Vendor", "Vendor")]
    [InlineData("pay_statement", "pay statement")]
    [InlineData("Pay statement", "Pay statement")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void ASourceKind_ShowsAsWords(string sourceKind, string expected)
        => Assert.Equal(expected, SourceKindText.Describe(sourceKind));
}
