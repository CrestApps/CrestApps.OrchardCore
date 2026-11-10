using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Builds the store's UCP profile, at <c>/.well-known/ucp</c>, from what the enabled features offer.
/// </summary>
public sealed class UcpProfileBuilder
{
    private readonly IEnumerable<IUcpProfileContributor> _contributors;
    private readonly IAgentCheckoutService _checkouts;
    private readonly IMerchantSigningKeyService _keys;
    private readonly AgentSiteUrls _urls;

    /// <summary>
    /// Initializes a new instance of the <see cref="UcpProfileBuilder"/> class.
    /// </summary>
    /// <param name="contributors">What the features offer.</param>
    /// <param name="checkouts">The agent checkout, for the payment handlers.</param>
    /// <param name="keys">The store's signing keys.</param>
    /// <param name="urls">The agent addresses.</param>
    public UcpProfileBuilder(
        IEnumerable<IUcpProfileContributor> contributors,
        IAgentCheckoutService checkouts,
        IMerchantSigningKeyService keys,
        AgentSiteUrls urls)
    {
        _contributors = contributors;
        _checkouts = checkouts;
        _keys = keys;
        _urls = urls;
    }

    /// <summary>
    /// Returns what the store offers for a version.
    /// </summary>
    /// <param name="version">The version.</param>
    public async Task<UcpProfileContext> GetOfferAsync(string version)
    {
        var context = new UcpProfileContext
        {
            Version = version,
            BaseUrl = await _urls.GetBaseUrlAsync(),
        };

        foreach (var contributor in _contributors)
        {
            await contributor.ContributeAsync(context);
        }

        return context;
    }

    /// <summary>
    /// Returns the store's profile for a version; the newest version's profile lists the older versions' addresses.
    /// </summary>
    /// <param name="version">The version.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<JsonObject> BuildAsync(string version, CancellationToken cancellationToken = default)
    {
        var offer = await GetOfferAsync(version);
        var services = new JsonArray();

        foreach (var service in offer.Services)
        {
            var entry = new JsonObject
            {
                ["version"] = version,
                ["transport"] = service.Transport,
                ["spec"] = UcpConstants.Url(version, "specification/overview/"),
            };

            if (!string.IsNullOrEmpty(service.Schema))
            {
                entry["schema"] = service.Schema;
            }

            if (!string.IsNullOrEmpty(service.Endpoint))
            {
                entry["endpoint"] = service.Endpoint;
            }

            if (service.Config is not null)
            {
                entry["config"] = service.Config.DeepClone();
            }

            services.Add(entry);
        }

        var capabilities = new JsonObject();

        foreach (var capability in offer.Capabilities)
        {
            var entry = new JsonObject
            {
                ["version"] = capability.Version,
                ["spec"] = capability.Spec,
                ["schema"] = capability.Schema,
            };

            if (capability.Extends.Count == 1)
            {
                entry["extends"] = capability.Extends[0];
            }
            else if (capability.Extends.Count > 1)
            {
                entry["extends"] = new JsonArray([.. capability.Extends.Select(name => (JsonNode)name)]);
            }

            if (capability.Config is not null)
            {
                entry["config"] = capability.Config.DeepClone();
            }

            capabilities[capability.Name] = new JsonArray(entry);
        }

        var ucp = new JsonObject
        {
            ["version"] = version,
        };

        if (version == UcpConstants.Versions[0])
        {
            var older = new JsonObject();

            foreach (var supported in UcpConstants.Versions.Skip(1))
            {
                older[supported] = $"{offer.BaseUrl}/{UcpConstants.ProfilePath}/{supported}";
            }

            ucp["supported_versions"] = older;
        }

        ucp["services"] = new JsonObject
        {
            ["dev.ucp.shopping"] = services,
        };
        ucp["capabilities"] = capabilities;
        ucp["payment_handlers"] = WritePaymentHandlers(await _checkouts.GetPaymentHandlersAsync(new AgentCheckoutContext
        {
            Protocol = AgenticCommerceConstants.Protocols.Ucp,
            ProtocolVersion = version,
        }, view: null, cancellationToken));

        var keys = new JsonArray();

        foreach (var key in await _keys.GetPublishedKeysAsync(cancellationToken))
        {
            keys.Add(key.ToJson(includePrivateKey: false));
        }

        return new JsonObject
        {
            ["ucp"] = ucp,
            ["keys"] = keys,
        };
    }

    /// <summary>
    /// Returns payment handlers in the shape of a profile or a response.
    /// </summary>
    /// <param name="handlers">The handlers.</param>
    public static JsonObject WritePaymentHandlers(IEnumerable<AgentPaymentHandlerDescriptor> handlers)
    {
        var result = new JsonObject();

        foreach (var group in (handlers ?? []).GroupBy(handler => handler.Name, StringComparer.Ordinal))
        {
            var entries = new JsonArray();

            foreach (var handler in group)
            {
                entries.Add(new JsonObject
                {
                    ["id"] = handler.Id,
                    ["version"] = handler.Version,
                    ["spec"] = handler.Spec,
                    ["schema"] = handler.Schema,
                    ["available_instruments"] = new JsonArray([.. handler.InstrumentTypes.Select(type => (JsonNode)new JsonObject { ["type"] = type })]),
                    ["config"] = handler.Config?.DeepClone() ?? new JsonObject(),
                });
            }

            result[group.Key] = entries;
        }

        return result;
    }
}
