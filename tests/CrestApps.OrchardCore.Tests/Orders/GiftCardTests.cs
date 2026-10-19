using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.Localization;
using Moq;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// Gift card codes, balances, and what a card offers to pay at checkout.
/// </summary>
public sealed class GiftCardTests
{
    private static readonly DateTime _now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ACode_IsSixteenUnambiguousCharactersInGroupsOfFour_AndIsFoundHoweverItsTyped()
    {
        var code = GiftCardCodes.Generate();

        Assert.Matches("^[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}$", code);
        Assert.Equal(GiftCardCodes.Normalize(code), GiftCardCodes.Normalize(code.ToLowerInvariant().Replace("-", " ")));
        Assert.Equal("ABCD-EFGH-JKLM-NPQR", GiftCardCodes.Format("abcd efgh jklm npqr"));
    }

    [Fact]
    public void ABalance_NeverGoesBelowZero_AndEveryChangeIsRecorded()
    {
        var card = new GiftCard { Currency = "USD", Code = "ABCD-EFGH-JKLM-4F2K" };

        card.ChangeBalance(50m, "Issued", _now);
        card.ChangeBalance(-80m, "Spent at checkout", _now, "session-1");

        Assert.Equal(0m, card.Balance);
        Assert.Equal(2, card.Activity.Count);
        Assert.Equal(0m, card.Activity[1].Balance);
        Assert.Equal("4F2K", card.LastCharacters);
    }

    [Fact]
    public async Task ACard_OffersItsBalance_OnlyInItsCurrency_WhileEnabledAndNotExpired()
    {
        var cards = new[]
        {
            new GiftCard { ItemId = "usd", Code = "AAAA-AAAA-AAAA-AAAA", Currency = "USD", Balance = 40m },
            new GiftCard { ItemId = "eur", Code = "BBBB-BBBB-BBBB-BBBB", Currency = "EUR", Balance = 40m },
            new GiftCard { ItemId = "off", Code = "CCCC-CCCC-CCCC-CCCC", Currency = "USD", Balance = 40m, IsEnabled = false },
            new GiftCard { ItemId = "old", Code = "DDDD-DDDD-DDDD-DDDD", Currency = "USD", Balance = 40m, ExpiresUtc = _now.AddDays(-1) },
        };

        var store = new Mock<IGiftCardStore>();
        store.Setup(value => value.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) => cards.Where(card => ids.Contains(card.ItemId)).ToArray());

        var provider = new GiftCardTenderProvider(store.Object, new TestClock(_now), new PassThroughLocalizer<GiftCardTenderProvider>());
        var invoice = new CheckoutInvoice { Currency = "USD" };

        var lines = await provider.GetTendersAsync(new CheckoutTenderContext(invoice, new CheckoutFlow(new CheckoutSession()), ["usd", "eur", "off", "old"]), TestContext.Current.CancellationToken);

        var line = Assert.Single(lines);
        Assert.Equal("usd", line.Reference);
        Assert.Equal(40m, line.Amount);
        Assert.Equal("Gift card ending AAAA", line.Description);
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
