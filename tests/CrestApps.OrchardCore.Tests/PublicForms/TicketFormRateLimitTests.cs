using System.Net;
using System.Threading.RateLimiting;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.WebForm;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.RateLimits;
using TicketsRateLimitsStartup = CrestApps.OrchardCore.Tickets.Portal.RateLimitsStartup;

namespace CrestApps.OrchardCore.Tests.PublicForms;

/// <summary>
/// The Ticket Form widget's limit per visitor is Orchard Core's Rate Limits group of the form, held across nodes: a
/// visitor sends as many requests in ten minutes as the in-memory counter of each node let them before, and a refused
/// post goes back to the page instead of an error page.
/// </summary>
public sealed class TicketFormRateLimitTests
{
    [Fact]
    public void AVisitor_IsHeldToFiveRequestsInTenMinutes_ByTheFormsRateLimitsGroup()
    {
        // Arrange
        var options = Options();
        var limit = Assert.Single(options.GroupRateLimits, limit => limit.GroupName == TicketsConstants.RateLimitGroups.WebForm);

        using var visitor = Limiter(limit, "203.0.113.7");
        using var other = Limiter(limit, "203.0.113.8");

        // Act
        var allowed = Enumerable.Range(0, TicketsRateLimitsStartup.WebFormPostsPerTenMinutes)
            .Select(_ =>
            {
                using var lease = visitor.AttemptAcquire();

                return lease.IsAcquired;
            })
            .ToList();

        using var refused = visitor.AttemptAcquire();
        using var otherVisitor = other.AttemptAcquire();

        // Assert
        Assert.Equal(5, TicketsRateLimitsStartup.WebFormPostsPerTenMinutes);
        Assert.All(allowed, Assert.True);
        Assert.False(refused.IsAcquired);
        Assert.True(otherVisitor.IsAcquired);
    }

    [Fact]
    public void ARefusedFormPost_HasTheFormsOwnAnswer()
    {
        // Act
        var registered = Options().TryGetRouteRejectionHandler(TicketFormController.SubmitRouteName, out var handler);

        // Assert
        Assert.True(registered);
        Assert.NotNull(handler);
    }

    [Theory]
    [InlineData("/help/contact", "/help/contact?ticketForm=w1&ticketError=busy#ticket-form-w1")]
    [InlineData("//evil.example/page", "/?ticketForm=w1&ticketError=busy#ticket-form-w1")]
    [InlineData("https://evil.example/page", "/?ticketForm=w1&ticketError=busy#ticket-form-w1")]
    public async Task ARefusedFormPost_GoesBackToItsPage_AndOnlyToAPageOfTheSite(string returnUrl, string expected)
    {
        // Arrange
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IUrlHelperFactory, UrlHelperFactory>()
                .BuildServiceProvider(),
        };
        httpContext.Request.QueryString = QueryString.Create("returnUrl", returnUrl);
        httpContext.Request.RouteValues["widgetId"] = "w1";

        // Act
        await TicketFormController.OnRateLimitedAsync(new OnRejectedContext { HttpContext = httpContext, Lease = new RefusedLease() }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal(expected, httpContext.Response.Headers.Location.ToString());
    }

    private static RateLimitsOptions Options()
    {
        var services = new ServiceCollection();

        new TicketsRateLimitsStartup().ConfigureServices(services);

        return services.BuildServiceProvider().GetRequiredService<IOptions<RateLimitsOptions>>().Value;
    }

    private static RateLimiter Limiter(GroupRateLimit limit, string address)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(address);

        var partition = limit.Partitioner(httpContext);

        return partition.Factory(partition.PartitionKey);
    }

    private sealed class RefusedLease : RateLimitLease
    {
        public override bool IsAcquired => false;

        public override IEnumerable<string> MetadataNames => [];

        public override bool TryGetMetadata(string metadataName, out object metadata)
        {
            metadata = null;

            return false;
        }
    }
}
