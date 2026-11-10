using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Builds the A2A agent card of the store's commerce agent: an A2A 0.3 card whose UCP extension, one per served
/// version, lists the capabilities the agent offers in <c>params.capabilities</c>.
/// </summary>
public static class UcpA2aAgentCard
{
    // Catalog and cart have no A2A binding in UCP, so the agent doesn't offer them.
    private static readonly HashSet<string> _notOffered = new(StringComparer.Ordinal)
    {
        UcpConstants.Capabilities.Cart,
        UcpConstants.Capabilities.CatalogSearch,
        UcpConstants.Capabilities.CatalogLookup,
    };

    /// <summary>
    /// Returns the agent card.
    /// </summary>
    /// <param name="baseUrl">The tenant's absolute base address, with no trailing slash.</param>
    /// <param name="storeName">The store's name.</param>
    /// <param name="offers">What the store offers for each version it serves, newest first.</param>
    public static JsonObject Build(string baseUrl, string storeName, IEnumerable<UcpProfileContext> offers)
    {
        ArgumentNullException.ThrowIfNull(offers);

        var offered = offers.ToList();
        var endpoint = $"{baseUrl}/{UcpA2a.Route}";
        var name = string.IsNullOrWhiteSpace(storeName) ? "Store" : storeName.Trim();

        var card = new JsonObject
        {
            ["protocolVersion"] = UcpA2a.ProtocolVersion,
            ["name"] = $"{name} shopping agent",
            ["description"] = $"Checks out and follows orders at {name} for a buyer, with the Universal Commerce Protocol (UCP). Send UCP actions and payloads in data parts.",
            ["url"] = endpoint,
            ["preferredTransport"] = "JSONRPC",
            ["additionalInterfaces"] = new JsonArray(new JsonObject { ["url"] = endpoint, ["transport"] = "JSONRPC" }),
            ["version"] = offered.FirstOrDefault()?.Version ?? UcpConstants.Versions[0],
            ["provider"] = new JsonObject
            {
                ["organization"] = name,
                ["url"] = baseUrl,
            },
            ["capabilities"] = new JsonObject
            {
                ["streaming"] = false,
                ["pushNotifications"] = false,
                ["stateTransitionHistory"] = false,
                ["extensions"] = WriteExtensions(offered),
            },

            // UCP's own schema for the card reads the extensions at the top level; A2A reads them under capabilities.
            ["extensions"] = WriteExtensions(offered),
            ["defaultInputModes"] = new JsonArray("application/json", "text/plain"),
            ["defaultOutputModes"] = new JsonArray("application/json"),
            ["skills"] = new JsonArray(
                new JsonObject
                {
                    ["id"] = "checkout",
                    ["name"] = "Checkout",
                    ["description"] = "Creates, reads, updates, completes and cancels UCP checkout sessions. Send a data part whose action is " +
                        $"{UcpA2a.Actions.CreateCheckout}, {UcpA2a.Actions.GetCheckout}, {UcpA2a.Actions.UpdateCheckout}, {UcpA2a.Actions.CompleteCheckout}, " +
                        $"{UcpA2a.Actions.CancelCheckout} or {UcpA2a.Actions.AddToCheckout}, with the checkout under {UcpA2a.DataKeys.Checkout} and the payment under {UcpA2a.DataKeys.Payment}.",
                    ["tags"] = new JsonArray("ucp", "checkout", "shopping"),
                    ["inputModes"] = new JsonArray("application/json"),
                    ["outputModes"] = new JsonArray("application/json"),
                },
                new JsonObject
                {
                    ["id"] = "order",
                    ["name"] = "Orders",
                    ["description"] = $"Returns an order the platform's agents placed. Send a data part whose action is {UcpA2a.Actions.GetOrder}, with the order's id.",
                    ["tags"] = new JsonArray("ucp", "order", "shopping"),
                    ["inputModes"] = new JsonArray("application/json"),
                    ["outputModes"] = new JsonArray("application/json"),
                }),
            ["securitySchemes"] = new JsonObject
            {
                ["apiKey"] = new JsonObject
                {
                    ["type"] = "apiKey",
                    ["in"] = "header",
                    ["name"] = "X-API-Key",
                    ["description"] = "The platform's API key from the store.",
                },
                ["bearer"] = new JsonObject
                {
                    ["type"] = "http",
                    ["scheme"] = "bearer",
                    ["description"] = "An OAuth access token from the store's OpenID server, or the platform's API key. Platforms may instead sign requests (RFC 9421) with a key from their UCP profile.",
                },
            },
            ["security"] = new JsonArray(
                new JsonObject { ["apiKey"] = new JsonArray() },
                new JsonObject { ["bearer"] = new JsonArray() }),
        };

        return card;
    }

    private static JsonArray WriteExtensions(IEnumerable<UcpProfileContext> offers)
    {
        var extensions = new JsonArray();

        foreach (var offer in offers)
        {
            var capabilities = new JsonObject();

            foreach (var capability in offer.Capabilities.Where(capability => !_notOffered.Contains(capability.Name)))
            {
                var entry = new JsonObject
                {
                    ["version"] = capability.Version,
                };

                if (capability.Extends.Count == 1)
                {
                    entry["extends"] = capability.Extends[0];
                }
                else if (capability.Extends.Count > 1)
                {
                    entry["extends"] = new JsonArray([.. capability.Extends.Select(parent => (JsonNode)parent)]);
                }

                capabilities[capability.Name] = new JsonArray(entry);
            }

            extensions.Add(new JsonObject
            {
                ["uri"] = UcpA2a.ExtensionUri(offer.Version),
                ["description"] = $"Business agent supporting UCP {offer.Version}",
                ["required"] = false,
                ["params"] = new JsonObject
                {
                    ["capabilities"] = capabilities,
                },
            });
        }

        return extensions;
    }
}
