using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// RL-6: the setup guide's "Protect payments" step is done once Payment Rate Limits is on, and warns a store behind a
/// proxy it doesn't trust, where every buyer would share the proxy's limit.
/// </summary>
public sealed class ProtectPaymentsSetupStepTests
{
    [Fact]
    public async Task WithoutPaymentRateLimits_TheStepIsNotDone()
    {
        var description = await DescribeAsync(enabled: [], forwarded: false);

        Assert.False(description.IsDone);
        Assert.Equal("Payment Rate Limits is off.", description.Status.Value);
    }

    [Fact]
    public async Task WithPaymentRateLimits_TheStepIsDone()
    {
        var description = await DescribeAsync(enabled: [CheckoutConstants.Features.RateLimits], forwarded: false);

        Assert.True(description.IsDone);
    }

    [Fact]
    public async Task BehindAProxyWithoutReverseProxy_TheStepWarns()
    {
        var description = await DescribeAsync(enabled: [CheckoutConstants.Features.RateLimits], forwarded: true);

        Assert.False(description.IsDone);
        Assert.Contains("Reverse Proxy", description.Status.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BehindATrustedProxy_TheStepIsDone()
    {
        var description = await DescribeAsync(enabled: [CheckoutConstants.Features.RateLimits, OrchardCoreFeatureIds.ReverseProxy], forwarded: true);

        Assert.True(description.IsDone);
    }

    private static async Task<global::CrestApps.OrchardCore.Commerce.StoreSetupStepDescription> DescribeAsync(string[] enabled, bool forwarded)
    {
        var features = new Mock<IShellFeaturesManager>();
        features.Setup(manager => manager.GetEnabledFeaturesAsync())
            .ReturnsAsync(enabled.Select(id => Mock.Of<IFeatureInfo>(feature => feature.Id == id)).ToList());

        var httpContext = new DefaultHttpContext();

        if (forwarded)
        {
            httpContext.Request.Headers["X-Forwarded-For"] = "203.0.113.7";
        }

        var step = new ProtectPaymentsSetupStep(
            features.Object,
            Mock.Of<LinkGenerator>(),
            Mock.Of<IHttpContextAccessor>(accessor => accessor.HttpContext == httpContext),
            new PassThroughLocalizer());

        return await step.DescribeAsync(TestContext.Current.CancellationToken);
    }

    private sealed class PassThroughLocalizer : IStringLocalizer<ProtectPaymentsSetupStep>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
