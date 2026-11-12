using System.Net;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Core.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Telnyx;

/// <summary>
/// DRY-7: the startup added the resilience handler to the client named "Telnyx", but registered the typed
/// <see cref="TelnyxApiClient"/> (every call-control command) under its own type name, so the commands went out with no
/// timeout and no circuit breaker although the comment said they inherited them. The typed client now has them, without
/// a transport retry: its own policy decides per command whether repeating is safe, so a GET is not repeated at both
/// layers.
/// </summary>
public sealed class TelnyxHttpClientRegistrationTests
{
    [Fact]
    public async Task TheTypedApiClient_GoesThroughAResilienceHandler_WithoutATransportRetry()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        new global::CrestApps.OrchardCore.Telnyx.Startup().ConfigureServices(services);

        // The site settings the options are read from aren't part of this test.
        services.RemoveAll<IConfigureOptions<TelnyxOptions>>();
        services.RemoveAll<IOptionsChangeTokenSource<TelnyxOptions>>();

        var primary = new CountingHandler();
        services.AddHttpClient(nameof(TelnyxApiClient)).ConfigurePrimaryHttpMessageHandler(() => primary);

        using var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(nameof(TelnyxApiClient));
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(TelnyxApiClient));

        // Act: a status read the provider can't answer.
        using var response = await client.GetAsync("https://api.telnyx.com/v2/calls/abc", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(EgressNetworkTests.HandlerChain(handler), link => link is ResilienceHandler);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, primary.Count);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Count { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
