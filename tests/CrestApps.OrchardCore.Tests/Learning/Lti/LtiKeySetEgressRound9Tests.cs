using System.Net.Http;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Lti;

/// <summary>
/// Review round 9, M6: the client that reads a tool's key set from the JWKS URL an admin entered goes through the
/// egress guard, as every client of admin-entered URLs does, so it never reaches the site's own network.
/// </summary>
public sealed class LtiKeySetEgressRound9Tests
{
    [Fact]
    public async Task Round9M6_TheToolKeySetClient_RefusesAnAddressOnTheSitesOwnNetwork()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddLearningLtiCore();

        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(LtiToolDirectory.HttpClientName);

        // Act
        var exception = await Assert.ThrowsAnyAsync<HttpRequestException>(() => client.GetAsync("http://127.0.0.1:9/.well-known/jwks.json", TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains(EgressNetwork.RefusedMessage, exception.Message, StringComparison.Ordinal);
    }
}
