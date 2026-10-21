using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Reviews.Models;
using CrestApps.OrchardCore.Reviews.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Email;
using OrchardCore.Infrastructure;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// Buyers who accept marketing are asked once to review what they bought, some days after paying; review photos have to
/// be images by their bytes.
/// </summary>
public sealed class ReviewRequestTests
{
    private static readonly DateTime _now = new(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0 }, ".jpg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, ".png")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, ".webp")]
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67 }, null)]
    [InlineData(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, null)]
    public void APhoto_IsAnImageByItsFirstBytes(byte[] head, string extension)
        => Assert.Equal(extension, ReviewPhotoStore.GetExtension(head));

    [Fact]
    public async Task OnlyBuyersWhoAcceptMarketing_AreAsked_AndEachOrderOnce()
    {
        var yes = Order("yes", "sam@example.com");
        var no = Order("no", "kim@example.com");
        var recent = Order("yes", "sam@example.com", paidDaysAgo: 3);

        var orders = new Mock<IOrderStore>();
        orders.Setup(store => store.PageAsync(It.IsAny<OrderQuery>(), 0, 500, It.IsAny<CancellationToken>()))
            .ReturnsAsync((3, (IReadOnlyList<Order>)[yes, no, recent]));

        var customers = new Mock<ICustomerStore>();
        customers.Setup(store => store.FindByIdAsync("yes", It.IsAny<CancellationToken>())).ReturnsAsync(new Customer { ItemId = "yes", AcceptsMarketing = true });
        customers.Setup(store => store.FindByIdAsync("no", It.IsAny<CancellationToken>())).ReturnsAsync(new Customer { ItemId = "no", AcceptsMarketing = false });

        var sent = new List<MailMessage>();
        var email = new Mock<IEmailService>();
        email.Setup(value => value.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<MailMessage, string, CancellationToken>((message, _, _) => sent.Add(message))
            .ReturnsAsync(Result.Success());

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var links = new Mock<LinkGenerator>();
        links.Setup(value => value.GetPathByAddress(It.IsAny<RouteValuesAddress>(), It.IsAny<RouteValueDictionary>(), It.IsAny<PathString>(), It.IsAny<FragmentString>(), It.IsAny<LinkOptions>()))
            .Returns((RouteValuesAddress _, RouteValueDictionary values, PathString _, FragmentString _, LinkOptions _) => $"/Contents/ContentItems/{values["contentItemId"]}");

        var service = new ReviewRequestService(orders.Object, customers.Object, email.Object, SiteService(), clock.Object, NullLogger<ReviewRequestService>.Instance, new PassThroughStringLocalizer<ReviewRequestService>(), links.Object);

        Assert.Equal(1, await service.SendDueAsync(TestContext.Current.CancellationToken));

        var message = Assert.Single(sent);
        Assert.Equal("sam@example.com", message.To);
        Assert.Contains("/Contents/ContentItems/hat#reviews", message.HtmlBody);

        // Both due orders are marked, so neither is asked again; the recent one waits.
        Assert.Equal(_now, yes.ReviewRequestSentUtc);
        Assert.Equal(_now, no.ReviewRequestSentUtc);
        Assert.Null(recent.ReviewRequestSentUtc);
    }

    private static Order Order(string customerId, string email, int paidDaysAgo = 20)
        => new()
        {
            ItemId = Guid.NewGuid().ToString("N"),
            OrderNumber = "261001-" + customerId,
            CustomerId = customerId,
            CustomerEmail = email,
            Status = OrderStatus.Paid,
            PaidUtc = _now.AddDays(-paidDaysAgo),
            CreatedUtc = _now.AddDays(-paidDaysAgo),
            Lines = [new OrderLine { ContentItemId = "hat", Title = "Hat", Quantity = 1 }],
        };

    private static ISiteService SiteService()
    {
        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<ReviewSettings>()).Returns(new ReviewSettings { SendReviewRequests = true, ReviewRequestDelayDays = 14 });
        site.SetupGet(value => value.BaseUrl).Returns("https://store.example.com");
        site.SetupGet(value => value.SiteName).Returns("Contoso");
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return siteService.Object;
    }
}
