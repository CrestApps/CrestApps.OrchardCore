using System.Net;
using System.Net.Sockets;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.Tooling.Instances;
using CrestApps.Core.AI.Tooling.Instances.Documentation;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Tools.Drivers;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using Xunit;

namespace CrestApps.OrchardCore.Tests.AI;

/// <summary>
/// H4 and M8 of the final security review: the HTTP API Request tool's and the documentation sources' named clients, and
/// the Cartesia and ElevenLabs speech clients, were outside the AI egress guard, and the tool-instance editors checked a
/// base URL only for http or https. A tenant user could point an instance at the cloud metadata address and read the
/// answer in the chat. Provider endpoints were checked as text only, so a host name that resolves inward was saved.
/// </summary>
public sealed class AIToolInstanceEgressTests
{
    [Theory]
    [InlineData("CrestApps.Core.AI.HttpApiRequest")]
    [InlineData("CrestApps.Documentation")]
    [InlineData("CrestApps.Cartesia")]
    [InlineData("CrestApps.ElevenLabs")]
    public async Task TheToolInstancesFeature_GuardsThePackagesClients_AtConnect(string clientName)
    {
        // Arrange: the feature's real registration, where the package sets the documentation client's own primary handler.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Options.Create(new AIEgressOptions()));
        new ToolInstancesStartup().ConfigureServices(services);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);

        // Act
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://127.0.0.1:9/", TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains(EgressNetwork.RefusedMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddAIEgressGuards_WinsOverAPrimaryHandlerRegisteredAfterIt()
    {
        // Arrange: another feature (MCP, say) adds the guards before the package configures its own handler.
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(new AIEgressOptions()));
        services.AddAIEgressGuards();
        services.AddHttpClient("CrestApps.Documentation").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = true });
        services.AddAIEgressGuards();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("CrestApps.Documentation");

        // Act
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://127.0.0.1:9/", TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains(EgressNetwork.RefusedMessage, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://10.0.0.5/")]
    [InlineData("http://localhost:8080/")]
    public async Task HttpApiRequestEditor_RefusesABaseUrlOnTheSitesOwnNetwork(string baseUrl)
    {
        // Arrange
        var instance = new AIToolInstance { Source = HttpApiRequestToolConstants.SourceName };
        var context = CreateContext(new HttpApiRequestToolInstanceViewModel { BaseUrl = baseUrl, HttpMethod = "GET" });

        // Act
        await new HttpApiRequestToolInstanceDisplayDriver(new EphemeralDataProtectionProvider(), Policy(), new PassThroughLocalizer<HttpApiRequestToolInstanceDisplayDriver>())
            .UpdateAsync(instance, context);

        // Assert
        Assert.False(context.Updater.ModelState.IsValid);
        Assert.Contains(context.Updater.ModelState, entry => entry.Key.EndsWith(nameof(HttpApiRequestToolInstanceViewModel.BaseUrl), StringComparison.Ordinal));
    }

    [Fact]
    public async Task HttpApiRequestEditor_RefusesAnOAuth2TokenEndpointOnTheSitesOwnNetwork()
    {
        // Arrange
        var instance = new AIToolInstance { Source = HttpApiRequestToolConstants.SourceName };
        var context = CreateContext(new HttpApiRequestToolInstanceViewModel
        {
            BaseUrl = "https://api.example.com/",
            HttpMethod = "GET",
            AuthenticationType = HttpApiRequestAuthenticationType.OAuth2,
            TokenEndpoint = "http://169.254.169.254/token",
            ClientId = "client",
        });

        // Act
        await new HttpApiRequestToolInstanceDisplayDriver(new EphemeralDataProtectionProvider(), Policy(), new PassThroughLocalizer<HttpApiRequestToolInstanceDisplayDriver>())
            .UpdateAsync(instance, context);

        // Assert
        Assert.Contains(context.Updater.ModelState, entry => entry.Key.EndsWith(nameof(HttpApiRequestToolInstanceViewModel.TokenEndpoint), StringComparison.Ordinal) && entry.Value.Errors.Count > 0);
    }

    [Fact]
    public async Task HttpApiRequestEditor_AcceptsAPublicBaseUrl()
    {
        // Arrange
        var instance = new AIToolInstance { Source = HttpApiRequestToolConstants.SourceName };
        var context = CreateContext(new HttpApiRequestToolInstanceViewModel { BaseUrl = "https://api.example.com/orders", HttpMethod = "GET" });

        // Act
        await new HttpApiRequestToolInstanceDisplayDriver(new EphemeralDataProtectionProvider(), Policy(), new PassThroughLocalizer<HttpApiRequestToolInstanceDisplayDriver>())
            .UpdateAsync(instance, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
    }

    [Fact]
    public async Task DocumentationEditors_RefuseAnAddressOnTheSitesOwnNetwork()
    {
        // Arrange
        var sitemap = new AIToolInstance { Source = DocumentationToolConstants.SitemapSourceName };
        var sitemapContext = CreateContext(new SitemapDocumentationToolInstanceViewModel { BaseUrl = "https://docs.example.com/", SitemapUrl = "http://10.1.2.3/sitemap.xml" });
        var index = new AIToolInstance { Source = DocumentationToolConstants.SearchIndexSourceName };
        var indexContext = CreateContext(new SearchIndexDocumentationToolInstanceViewModel { BaseUrl = "http://169.254.169.254/" });
        var search = new AIToolInstance { Source = DocumentationToolConstants.WebsiteSearchSourceName };
        var searchContext = CreateContext(new WebsiteSearchToolInstanceViewModel { BaseUrl = "http://127.0.0.1/", SearchPath = "/s", QueryParameter = "q", TitlePath = "t", UrlPath = "u" });

        // Act
        await new SitemapDocumentationToolInstanceDisplayDriver(Policy(), new PassThroughLocalizer<SitemapDocumentationToolInstanceDisplayDriver>()).UpdateAsync(sitemap, sitemapContext);
        await new SearchIndexDocumentationToolInstanceDisplayDriver(Policy(), new PassThroughLocalizer<SearchIndexDocumentationToolInstanceDisplayDriver>()).UpdateAsync(index, indexContext);
        await new WebsiteSearchToolInstanceDisplayDriver(Policy(), new PassThroughLocalizer<WebsiteSearchToolInstanceDisplayDriver>()).UpdateAsync(search, searchContext);

        // Assert
        Assert.False(sitemapContext.Updater.ModelState.IsValid);
        Assert.False(indexContext.Updater.ModelState.IsValid);
        Assert.False(searchContext.Updater.ModelState.IsValid);
    }

    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    public void EndpointPolicy_RefusesAHostNameThatResolvesInward(string answer)
    {
        // Arrange (M8): the provider SDKs build their own transport, so the save-time check is the one they get.
        var policy = new AIEndpointPolicy(null, Options.Create(new AIEgressOptions()), _ => [IPAddress.Parse("93.184.215.14"), IPAddress.Parse(answer)]);

        // Act and assert
        Assert.False(policy.IsAllowed("https://models.attacker.example/v1"));
    }

    [Fact]
    public void EndpointPolicy_RefusesLocalhostWithATrailingDot()
    {
        var policy = new AIEndpointPolicy(null, Options.Create(new AIEgressOptions()), _ => [IPAddress.Loopback]);

        Assert.False(policy.IsAllowed("http://localhost.:8080/"));
    }

    [Fact]
    public void EndpointPolicy_AllowsAHostNameThatResolvesOutward_OrNotAtAll()
    {
        var resolvesOutward = new AIEndpointPolicy(null, Options.Create(new AIEgressOptions()), _ => [IPAddress.Parse("93.184.215.14")]);
        var doesNotResolve = new AIEndpointPolicy(null, Options.Create(new AIEgressOptions()), _ => throw new SocketException((int)SocketError.HostNotFound));

        Assert.True(resolvesOutward.IsAllowed("https://api.openai.com/v1"));
        Assert.True(doesNotResolve.IsAllowed("https://models.example/v1"));
    }

    [Fact]
    public void EndpointPolicy_WhenAPrivateNetworkIsAllowed_StillRefusesANameThatResolvesToMetadata()
    {
        var options = Options.Create(new AIEgressOptions { AllowPrivateNetwork = true });
        var resolvesPrivate = new AIEndpointPolicy(null, options, _ => [IPAddress.Parse("10.0.0.5")]);
        var resolvesMetadata = new AIEndpointPolicy(null, options, _ => [IPAddress.Parse("168.63.129.16")]);

        Assert.True(resolvesPrivate.IsAllowed("http://ollama.internal:11434/"));
        Assert.False(resolvesMetadata.IsAllowed("http://ollama.internal:11434/"));
    }

    [Theory]
    [InlineData("fd00:ec2::254")]
    [InlineData("168.63.129.16")]
    [InlineData("100.100.100.200")]
    [InlineData("192.0.0.192")]
    [InlineData("::ffff:100.100.100.200")]
    [InlineData("64:ff9b::a83f:8110")]
    public void IsRefused_AMetadataServiceOutsideLinkLocal_EvenOnAPrivateNetwork(string address)
    {
        // Low (AI): AllowPrivateNetwork still reached the metadata services that do not answer on a link-local address.
        Assert.True(EgressNetwork.IsRefused(IPAddress.Parse(address), allowLoopback: true, allowPrivateNetwork: true));
    }

    [Fact]
    public void IsAllowedEndpoint_AMetadataServiceLiteral_IsRefusedEvenOnAPrivateNetwork()
    {
        var options = new AIEgressOptions { AllowPrivateNetwork = true };

        Assert.False(AIEgress.IsAllowedEndpoint("http://168.63.129.16/machine", isDevelopment: false, options));
        Assert.False(AIEgress.IsAllowedEndpoint("http://[fd00:ec2::254]/latest/meta-data/", isDevelopment: false, options));
        Assert.True(AIEgress.IsAllowedEndpoint("http://10.0.0.5:11434/", isDevelopment: false, options));
    }

    // The policy the AI feature registers, on a production host that allows no private network; names resolve outward.
    private static AIEndpointPolicy Policy()
        => new(null, Options.Create(new AIEgressOptions()), host => host == "localhost" ? [IPAddress.Loopback] : [IPAddress.Parse("93.184.215.14")]);

    private static UpdateEditorContext CreateContext(object posted)
        => new(null, string.Empty, false, string.Empty, null, null, new FakeUpdateModel(posted));

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
