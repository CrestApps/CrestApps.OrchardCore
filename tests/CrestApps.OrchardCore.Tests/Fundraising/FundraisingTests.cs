using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Fundraising;
using CrestApps.OrchardCore.Fundraising.Handlers;
using CrestApps.OrchardCore.Fundraising.Models;
using CrestApps.OrchardCore.Fundraising.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Fundraising;

/// <summary>
/// A donation is the amount the donor chose, read the same in every language, at least the campaign's least, never
/// discounted, and not taken once the campaign has ended; progress is how far toward the goal it is.
/// </summary>
public sealed class FundraisingTests
{
    private static readonly DateTime _now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("25", 25)]
    [InlineData(" 40.5 ", 40.5)]
    [InlineData("10.555", 10.56)]
    public void AnAmount_IsReadInTheInvariantCulture(string value, decimal expected)
        => Assert.Equal(expected, DonationAmounts.Parse(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("25,50")]
    [InlineData("1,000")]
    [InlineData("ten")]
    public void AnAmountThatIsntOne_IsNotRead(string value)
        => Assert.Null(DonationAmounts.Parse(value));

    [Fact]
    public void Progress_IsHowFarTowardTheGoal_FromNoneToAll()
    {
        Assert.Equal(25, new CampaignProgress(2500m, 10000m, 3, "USD", 10).Percent);
        Assert.Equal(33, new CampaignProgress(3333.99m, 10000m, 3, "USD", 10).Percent);
        Assert.Equal(100, new CampaignProgress(12000m, 10000m, 9, "USD", 0).Percent);
        Assert.Equal(0, new CampaignProgress(500m, 0m, 1, "USD", null).Percent);
    }

    [Fact]
    public async Task ADonation_UnderTheLeast_IsRefused_AndOneOverIt_KeepsItsAmountAndTribute()
    {
        var handler = CreateHandler();
        var item = CreateCampaign(new FundraisingCampaignPart { MinimumAmount = 5m });

        var tooLittle = Prepare(item, "2");

        await handler.PrepareAsync(tooLittle, TestContext.Current.CancellationToken);

        Assert.NotNull(tooLittle.Problem);

        var gift = Prepare(item, "40", "  my grandmother  ");

        await handler.PrepareAsync(gift, TestContext.Current.CancellationToken);

        Assert.Null(gift.Problem);
        Assert.Equal("40", gift.Details[FundraisingConstants.AmountDetail]);
        Assert.Equal("my grandmother", gift.Details[FundraisingConstants.TributeDetail]);
    }

    [Fact]
    public async Task ADonation_IsPricedAtItsAmount_OneALine_AndNeverDiscounted()
    {
        var handler = CreateHandler();
        var item = CreateCampaign(new FundraisingCampaignPart());
        var context = Evaluate(item, "40");

        await handler.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.Null(context.Problem);
        Assert.Equal(40m, context.UnitPrice);
        Assert.Equal(1, context.MaxQuantity);
        Assert.True(context.ExcludeFromDiscounts);
        Assert.Contains(context.Details, detail => detail.Key == FundraisingConstants.AmountDetail);
    }

    [Fact]
    public async Task ACampaignThatHasEnded_DoesntTakeDonations()
    {
        var handler = CreateHandler();
        var item = CreateCampaign(new FundraisingCampaignPart { EndsUtc = _now.AddDays(-1) });
        var context = Evaluate(item, "40");

        await handler.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.NotNull(context.Problem);
    }

    private static DonationLineHandler CreateHandler()
        => new(Mock.Of<IClock>(clock => clock.UtcNow == _now), new PassThroughStringLocalizer<DonationLineHandler>());

    private static ContentItem CreateCampaign(FundraisingCampaignPart part)
    {
        var item = new ContentItem { ContentItemId = "library", DisplayText = "Books for the library" };

        item.Weld(part);

        return item;
    }

    private static CartLinePrepareContext Prepare(ContentItem item, string amount, string tribute = null)
    {
        var posted = new Dictionary<string, string> { [FundraisingConstants.AmountDetail] = amount };

        if (tribute is not null)
        {
            posted[FundraisingConstants.TributeDetail] = tribute;
        }

        return new CartLinePrepareContext { ContentItem = item, Title = item.DisplayText, Quantity = 1, PostedDetails = posted };
    }

    private static CartLineEvaluateContext Evaluate(ContentItem item, string amount)
        => new()
        {
            ContentItem = item,
            Title = item.DisplayText,
            Quantity = 1,
            UnitPrice = 25m,
            MaxQuantity = 99,
            Line = new CartLine { Details = new() { [FundraisingConstants.AmountDetail] = amount } },
        };
}
