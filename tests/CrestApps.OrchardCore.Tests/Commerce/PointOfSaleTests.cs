using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.PointOfSale;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Services;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// Cash at the till: what was handed over pays up to what's due, the rest is the change, and the drawer is expected to
/// hold the float, the cash kept and what was put in less what was taken out.
/// </summary>
public sealed class PointOfSaleTests
{
    [Theory]
    [InlineData(20, "USD", "2000")]
    [InlineData(12.5, "USD", "1250")]
    [InlineData(1000, "JPY", "1000")]
    public void CashHandedOver_IsItsOwnReference_InMinorUnits(decimal amount, string currency, string reference)
    {
        Assert.Equal(reference, PosCashTenderProvider.ToReference(amount, currency));
        Assert.Equal(amount, PosCashTenderProvider.FromReference(reference, currency));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-100")]
    [InlineData("0")]
    [InlineData("abc")]
    public void AReferenceThatIsntCash_IsNothing(string reference)
        => Assert.Null(PosCashTenderProvider.FromReference(reference, "USD"));

    [Fact]
    public async Task Cash_IsTakenOnlyAtARegister()
    {
        var provider = new PosCashTenderProvider(Mock.Of<IStringLocalizer<PosCashTenderProvider>>(localizer => localizer[It.IsAny<string>()] == new LocalizedString("Cash", "Cash")));
        var invoice = new CheckoutInvoice { Currency = "USD" };
        var online = new CheckoutSession { SessionId = "online" };
        var counter = new CheckoutSession { SessionId = "counter" };
        counter.Put(new CheckoutCounterSale { RegisterId = "register-1" });

        var none = await provider.GetTendersAsync(new CheckoutTenderContext(invoice, new CheckoutFlow(online), ["2000"]));
        var some = await provider.GetTendersAsync(new CheckoutTenderContext(invoice, new CheckoutFlow(counter), ["2000"]));

        Assert.Empty(none);
        Assert.Equal(20m, some.Single().Amount);
    }

    [Fact]
    public void TheChange_IsWhatWasHandedOverLessWhatItPaid()
    {
        var session = new CheckoutSession();
        var selection = new CheckoutTenderSelection();
        selection.Add(PosConstants.CashTenderKey, "500");
        selection.Add(PosConstants.CashTenderKey, "2000");
        session.Put(selection);

        var invoice = new CheckoutInvoice
        {
            Currency = "USD",
            Tenders =
            [
                new TenderLine { ProviderKey = PosConstants.CashTenderKey, Reference = "500", Amount = 5m },
                new TenderLine { ProviderKey = PosConstants.CashTenderKey, Reference = "2000", Amount = 11m },
            ],
        };

        Assert.Equal(9m, PosSaleService.GetChange(session, invoice));
    }

    [Fact]
    public void TheDrawer_ShouldHoldTheFloat_TheCashKept_AndWhatWasPutInLessTakenOut()
    {
        var shift = new RegisterShift
        {
            OpeningFloat = 100m,
            CashMovements =
            [
                new RegisterCashMovement { Amount = 20m, Reason = "Change from the bank" },
                new RegisterCashMovement { Amount = -16m, Reason = "Refund of order 1" },
            ],
        };

        Assert.Equal(170m, shift.GetExpectedCash(cashKeptForSales: 66m));
    }
}
