using System.Net;
using System.Text;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Settings;
using Stripe;

namespace CrestApps.OrchardCore.Tests.Marketplace;

/// <summary>
/// A vendor whose Stripe account Stripe refused to make can try again: the retry isn't answered from Stripe's record of
/// the refused request.
/// </summary>
public sealed class StripeConnectVendorServiceTests
{
    [Fact]
    public async Task ARefusedAccount_IsTriedAgain_WithANewIdempotencyKey()
    {
        var handler = new StripeHandler();
        var vendor = new Vendor { ItemId = "v1", Name = "Northwind Pottery", Email = "pottery@example.test", Country = "US" };
        var service = CreateService(handler);

        // Connect isn't on yet: Stripe refuses the account.
        handler.Refuse = true;

        await Assert.ThrowsAsync<StripeException>(() => service.GetOnboardingUrlAsync(vendor, "https://store.test/back", "https://store.test/back", TestContext.Current.CancellationToken));
        Assert.Null(vendor.StripeAccountId);
        Assert.Null(vendor.StripeAccountRequestKey);

        // Connect is on: the retry makes the account and the onboarding link.
        handler.Refuse = false;

        var url = await service.GetOnboardingUrlAsync(vendor, "https://store.test/back", "https://store.test/back", TestContext.Current.CancellationToken);

        Assert.Equal("https://connect.stripe.com/setup/e/acct_123/onboard", url);
        Assert.Equal("acct_123", vendor.StripeAccountId);
        Assert.Null(vendor.StripeAccountRequestKey);

        var keys = handler.AccountRequestKeys;

        Assert.Equal(2, keys.Count);
        Assert.All(keys, key => Assert.StartsWith("vendor-account-v1-", key));
        Assert.NotEqual(keys[0], keys[1]);
    }

    private static StripeConnectVendorService CreateService(StripeHandler handler)
    {
        var client = new StripeClient("sk_test_vendor", httpClient: new SystemNetHttpClient(new HttpClient(handler), maxNetworkRetries: 0));

        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<MarketplaceSettings>()).Returns(new MarketplaceSettings());

        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new StripeConnectVendorService(client, Mock.Of<IVendorStore>(), siteService.Object, NullLogger<StripeConnectVendorService>.Instance);
    }

    /// <summary>
    /// Answers Stripe's account and account link calls, refusing accounts while <see cref="Refuse"/> is set.
    /// </summary>
    private sealed class StripeHandler : HttpMessageHandler
    {
        public bool Refuse { get; set; }

        public List<string> AccountRequestKeys { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri.AbsolutePath;

            if (path == "/v1/accounts")
            {
                AccountRequestKeys.Add(request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.First() : null);

                return Task.FromResult(Refuse
                    ? Json(HttpStatusCode.BadRequest, """{"error":{"type":"invalid_request_error","message":"You can only create new accounts if you've signed up for Connect."}}""")
                    : Json(HttpStatusCode.OK, """{"id":"acct_123","object":"account","type":"express"}"""));
            }

            if (path == "/v1/account_links")
            {
                return Task.FromResult(Json(HttpStatusCode.OK, """{"object":"account_link","url":"https://connect.stripe.com/setup/e/acct_123/onboard"}"""));
            }

            return Task.FromResult(Json(HttpStatusCode.NotFound, """{"error":{"type":"invalid_request_error","message":"Unexpected call."}}"""));
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body)
            => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
