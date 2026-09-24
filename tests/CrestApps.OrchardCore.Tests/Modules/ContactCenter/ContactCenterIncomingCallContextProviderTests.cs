using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The accept and decline actions, and the matched customers' links, an offer carries to the soft phone.
/// </summary>
public sealed class ContactCenterIncomingCallContextProviderTests
{
    [Fact]
    public async Task ContributeAsync_WhenTheRequestTheOfferStartedOnHasEnded_StillCarriesEveryAction()
    {
        // Arrange
        // An AI handoff offers the call from work that outlives the webhook request, and the accessor can still hand
        // back that request once it is disposed. Building the links from it threw, and the offer reached the soft
        // phone with no way to accept or decline it.
        var disposed = new DefaultHttpContext();
        disposed.Uninitialize();

        var provider = CreateProvider(disposed, requestUrlPrefix: "tenant-a");
        var context = CreateContext();

        // Act
        await provider.ContributeAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("/tenant-a/offers/reservation-1/accept", context.Properties["acceptUrl"]);
        Assert.Equal("/tenant-a/offers/reservation-1/decline", context.Properties["declineUrl"]);
        Assert.Equal("reservation-1", context.Properties["reservationId"]);
        var card = Assert.Single(context.Cards);
        Assert.Equal("/tenant-a/contents/contact-1", card.Url);
    }

    [Fact]
    public async Task ContributeAsync_WithNoRequest_BuildsTheLinksUnderTheTenantPrefix()
    {
        // Arrange
        var provider = CreateProvider(httpContext: null, requestUrlPrefix: null);
        var context = CreateContext();

        // Act
        await provider.ContributeAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("/offers/reservation-1/accept", context.Properties["acceptUrl"]);
        Assert.Equal("/offers/reservation-1/decline", context.Properties["declineUrl"]);
        Assert.Equal("/contents/contact-1", Assert.Single(context.Cards).Url);
    }

    [Fact]
    public async Task ContributeAsync_WithALiveRequest_KeepsItsHostedPathBase()
    {
        // Arrange
        var live = new DefaultHttpContext();
        live.Request.PathBase = "/hosted/tenant-a";

        var provider = CreateProvider(live, requestUrlPrefix: "tenant-a");
        var context = CreateContext();

        // Act
        await provider.ContributeAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("/hosted/tenant-a/offers/reservation-1/accept", context.Properties["acceptUrl"]);
        Assert.Equal("/hosted/tenant-a/contents/contact-1", Assert.Single(context.Cards).Url);
    }

    private static IncomingCallContributionContext CreateContext()
        => new(new TelephonyCall
        {
            CallId = "caller-1",
            From = "+15125550100",
            Direction = CallDirection.Inbound,
        }, "user-1");

    private static ContactCenterIncomingCallContextProvider CreateProvider(HttpContext httpContext, string requestUrlPrefix)
    {
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1" });

        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager
            .Setup(manager => manager.FindPendingByAgentAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation
            {
                ItemId = "reservation-1",
                AgentId = "agent-1",
                ExpiresUtc = new DateTime(2026, 9, 24, 4, 10, 0, DateTimeKind.Utc),
            });

        var contactLookup = new Mock<IInboundContactLookup>();
        contactLookup
            .Setup(lookup => lookup.FindContactItemIdsAsync("+15125550100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["contact-1"]);

        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(manager => manager.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync([new ContentItem { ContentItemId = "contact-1", DisplayText = "A customer" }]);

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.SetupGet(accessor => accessor.HttpContext).Returns(httpContext);

        var localizer = new Mock<IStringLocalizer<ContactCenterIncomingCallContextProvider>>();
        localizer
            .Setup(value => value[It.IsAny<string>()])
            .Returns((string name) => new LocalizedString(name, name));

        var shellSettings = new ShellSettings
        {
            Name = "Default",
        };
        shellSettings.RequestUrlPrefix = requestUrlPrefix;

        return new ContactCenterIncomingCallContextProvider(
            agentManager.Object,
            reservationManager.Object,
            new Mock<IAgentPreDialLegStore>().Object,
            new Mock<IActivityQueueManager>().Object,
            contactLookup.Object,
            contentManager.Object,
            httpContextAccessor.Object,
            new PathLinkGenerator(),
            shellSettings,
            localizer.Object);
    }

    /// <summary>
    /// Builds a path from the address and the path base, and reads the request the way the real generator does when
    /// it is handed one, so a disposed request fails here exactly as it does in the host.
    /// </summary>
    private sealed class PathLinkGenerator : LinkGenerator
    {
        public override string GetPathByAddress<TAddress>(
            HttpContext httpContext,
            TAddress address,
            RouteValueDictionary values,
            RouteValueDictionary ambientValues = null,
            PathString? pathBase = null,
            FragmentString fragment = default,
            LinkOptions options = null)
            => (pathBase ?? httpContext.Request.PathBase) + Describe(address, values);

        public override string GetPathByAddress<TAddress>(
            TAddress address,
            RouteValueDictionary values,
            PathString pathBase = default,
            FragmentString fragment = default,
            LinkOptions options = null)
            => pathBase + Describe(address, values);

        public override string GetUriByAddress<TAddress>(
            HttpContext httpContext,
            TAddress address,
            RouteValueDictionary values,
            RouteValueDictionary ambientValues = null,
            string scheme = null,
            HostString? host = null,
            PathString? pathBase = null,
            FragmentString fragment = default,
            LinkOptions options = null)
            => throw new NotSupportedException();

        public override string GetUriByAddress<TAddress>(
            TAddress address,
            RouteValueDictionary values,
            string scheme,
            HostString host,
            PathString pathBase = default,
            FragmentString fragment = default,
            LinkOptions options = null)
            => throw new NotSupportedException();

        private static string Describe<TAddress>(TAddress address, RouteValueDictionary values)
            => address switch
            {
                string name when name == "ContactCenterVoiceAcceptOffer" => $"/offers/{values["reservationId"]}/accept",
                string name when name == "ContactCenterVoiceDeclineOffer" => $"/offers/{values["reservationId"]}/decline",
                RouteValuesAddress routeValues => $"/contents/{routeValues.ExplicitValues["contentItemId"]}",
                _ => throw new NotSupportedException($"No test route for '{address}'."),
            };
    }
}
