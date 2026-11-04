using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Commerce.B2B;

/// <summary>
/// OCI punchout: a procurement system opens the store with the account's key and its HOOK_URL, and gets the cart back
/// as OCI fields.
/// </summary>
public sealed class PunchoutTests
{
    [Fact]
    public void OnlyTheAccountsCurrentKey_StartsAPunchout()
    {
        var (key, hash) = PunchoutService.NewKey();
        var account = TestB2BAccounts.Create(configure: part => part.Punchout = new AccountPunchout { Enabled = true, KeyHash = hash });

        Assert.Equal(43, key.Length);
        Assert.True(PunchoutService.IsKey(account, key));
        Assert.False(PunchoutService.IsKey(account, key + "x"));
        Assert.False(PunchoutService.IsKey(account, null));

        account.Punchout.Enabled = false;

        Assert.False(PunchoutService.IsKey(account, key));
        Assert.NotEqual(hash, PunchoutService.NewKey().Hash);
    }

    [Theory]
    [InlineData("https://procurement.example.com/oci/hook?session=1", true)]
    [InlineData("http://localhost:5399/hook", true)]
    [InlineData("http://procurement.example.com/hook", false)]
    [InlineData("https://user:secret@procurement.example.com/hook", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("/relative", false)]
    [InlineData(null, false)]
    public void TheCartGoesBack_OnlyToAnHttpsHookUrl(string value, bool accepted)
        => Assert.Equal(accepted, PunchoutService.ParseHookUrl(value) is not null);

    [Fact]
    public void TheCart_GoesBackAsNumberedOciFields_WithoutLinesThatCantBeBought()
    {
        var pricing = new CartPricing
        {
            Currency = "EUR",
            Lines =
            [
                new CartLinePricing
                {
                    Line = new CartLine { LineId = "a", ContentItemId = "hat", Quantity = 3 },
                    Title = "Tailspin Bucket Hat with a name long enough to need the long text",
                    UnitPrice = 18.99m,
                    LineTotal = 59.97m,
                },
                new CartLinePricing
                {
                    Line = new CartLine { LineId = "b", ContentItemId = "gone", Quantity = 1 },
                    Title = "Gone",
                    Problem = new LocalizedString("Unavailable", "Out of stock"),
                },
            ],
        };

        var fields = PunchoutService.ToOciFields(pricing).ToDictionary(field => field.Key, field => field.Value);

        Assert.Equal("Tailspin Bucket Hat with a name long eno", fields["NEW_ITEM-DESCRIPTION[1]"]);
        Assert.Equal("3", fields["NEW_ITEM-QUANTITY[1]"]);
        Assert.Equal("19.99", fields["NEW_ITEM-PRICE[1]"]);
        Assert.Equal("EUR", fields["NEW_ITEM-CURRENCY[1]"]);
        Assert.Equal("hat", fields["NEW_ITEM-EXT_PRODUCT_ID[1]"]);
        Assert.Equal("Tailspin Bucket Hat with a name long enough to need the long text", fields["NEW_ITEM-LONGTEXT_1:132[]"]);
        Assert.DoesNotContain("NEW_ITEM-DESCRIPTION[2]", fields.Keys);
    }
}
