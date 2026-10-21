using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Carts;

/// <summary>
/// A guest opens their order from a signed link that only this site could have made; a changed link opens nothing.
/// </summary>
public sealed class OrderLinkTests
{
    private static OrderLinkService CreateService(string baseUrl = "https://store.test/")
    {
        var site = new Mock<ISite>();
        site.Setup(s => s.BaseUrl).Returns(baseUrl);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        // The view route, as the endpoint it names would generate it.
        var links = new Mock<LinkGenerator>();
        links.Setup(value => value.GetPathByAddress(It.IsAny<RouteValuesAddress>(), It.IsAny<RouteValueDictionary>(), It.IsAny<PathString>(), It.IsAny<FragmentString>(), It.IsAny<LinkOptions>()))
            .Returns((RouteValuesAddress _, RouteValueDictionary values, PathString _, FragmentString _, LinkOptions _) => $"/orders/view/{Uri.EscapeDataString((string)values["token"])}");

        return new OrderLinkService(SignedLinkDoubles.Tokens(), SignedLinkDoubles.Urls(siteService.Object, links.Object));
    }

    [Fact]
    public async Task ALink_OpensTheOrderItNames()
    {
        var service = CreateService();

        var url = await service.CreateUrlAsync(new Order { ItemId = "order-1" });

        Assert.StartsWith("https://store.test/orders/view/", url);
        Assert.Equal("order-1", service.GetOrderId(Uri.UnescapeDataString(url["https://store.test/orders/view/".Length..])));
    }

    [Fact]
    public async Task AChangedLink_OpensNothing()
    {
        var service = CreateService();
        var url = await service.CreateUrlAsync(new Order { ItemId = "order-1" });
        var token = Uri.UnescapeDataString(url["https://store.test/orders/view/".Length..]);

        Assert.Null(service.GetOrderId(token[..^2] + (token.EndsWith('A') ? "BB" : "AA")));
        Assert.Null(service.GetOrderId("not-a-token"));
        Assert.Null(service.GetOrderId(null));
    }

    [Fact]
    public async Task WithoutABaseUrlOrARequest_NoLinkIsMade()
    {
        Assert.Null(await CreateService(baseUrl: null).CreateUrlAsync(new Order { ItemId = "order-1" }));
    }

    [Fact]
    public void FindMyOrder_EmailsALinkToEachOrder()
    {
        var builder = new OrderLinksEmailBuilder(new PassThroughLocalizer());
        var orders = new List<(Order, string)>
        {
            (new Order { OrderNumber = "261017-AAA111", Currency = "USD", Total = 25m, CreatedUtc = new DateTime(2026, 10, 17) }, "https://store.test/orders/view/one"),
            (new Order { OrderNumber = "261001-BBB222", Currency = "USD", Total = 40m, CreatedUtc = new DateTime(2026, 10, 1) }, "https://store.test/orders/view/two"),
        };

        var email = builder.Build(orders);

        Assert.Contains("https://store.test/orders/view/one", email.Text);
        Assert.Contains("https://store.test/orders/view/two", email.Html);
        Assert.Contains("261001-BBB222", email.Html);
    }

    private sealed class PassThroughLocalizer : IStringLocalizer<OrderLinksEmailBuilder>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
