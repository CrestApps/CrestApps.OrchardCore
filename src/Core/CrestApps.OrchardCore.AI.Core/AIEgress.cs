using System.Net;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Host settings for the addresses AI features may reach, bound from the <c>CrestApps:AI:Egress</c> configuration
/// section, which only the host's operator controls.
/// </summary>
public sealed class AIEgressOptions
{
    /// <summary>
    /// The configuration section the options are bound from.
    /// </summary>
    public const string SectionName = "CrestApps:AI:Egress";

    /// <summary>
    /// Gets or sets a value indicating whether AI connections, crawlers and provider endpoints may reach private and
    /// loopback addresses, for a host that runs a local model host (such as Ollama) or local MCP servers on purpose. Off by
    /// default. The cloud metadata address is refused even when on.
    /// </summary>
    public bool AllowPrivateNetwork { get; set; }
}

/// <summary>
/// Keeps the endpoints AI features call (MCP HTTP connections, A2A agents, web crawlers, model providers) off the site's
/// own network, at save and at connect.
/// </summary>
public static class AIEgress
{
    /// <summary>
    /// The named HTTP clients the AI building blocks use, guarded by <see cref="AddAIEgressGuards"/>: the MCP, A2A and
    /// web crawler clients, and the one the package's OAuth2 token service fetches a connection's token endpoint with.
    /// </summary>
    public static readonly string[] GuardedClientNames = ["CrestApps.Mcp", "CrestApps.A2A", "CrestApps.WebCrawler", "DefaultOAuth2TokenService"];

    /// <summary>
    /// Whether an endpoint an administrator typed may be saved: http or https, never on the site's own network unless the
    /// host allows a private network. Plain http to a public host is allowed, as it is when the endpoint is called.
    /// </summary>
    /// <param name="url">The endpoint.</param>
    /// <param name="isDevelopment">Whether the site runs in development, where loopback over HTTP is allowed.</param>
    /// <param name="options">The host's options.</param>
    public static bool IsAllowedEndpoint(string url, bool isDevelopment, AIEgressOptions options)
    {
        if (options?.AllowPrivateNetwork != true)
        {
            return EgressNetwork.IsExternalUrl(url, isDevelopment);
        }

        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        return !(IPAddress.TryParse(uri.Host.Trim('[', ']'), out var literal) && EgressNetwork.IsLinkLocal(literal));
    }

    /// <summary>
    /// Replaces the primary handler of the AI building blocks' HTTP clients with one that checks every address it
    /// connects to, redirects included.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddAIEgressGuards(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (var name in GuardedClientNames)
        {
            services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(serviceProvider =>
            {
                var isDevelopment = serviceProvider.GetService<IHostEnvironment>()?.IsDevelopment() == true;
                var allowPrivateNetwork = serviceProvider.GetService<IOptions<AIEgressOptions>>()?.Value.AllowPrivateNetwork == true;

                // Crawled sites commonly redirect (sitemap.xml to sitemap_index.xml); every hop is checked at connect.
                return EgressNetwork.CreateHandler(isDevelopment, allowPrivateNetwork, allowRedirects: true);
            });
        }

        return services;
    }
}
