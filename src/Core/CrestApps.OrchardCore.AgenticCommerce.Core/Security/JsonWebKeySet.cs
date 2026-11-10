using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// Reads JSON Web Key sets, either a bare <c>[...]</c> array of keys (as in a UCP profile's <c>keys</c>) or a
/// <c>{ "keys": [...] }</c> JWKS document.
/// </summary>
public static class JsonWebKeySet
{
    /// <summary>
    /// Reads the EC keys of a key set and skips keys of other types and keys that are not valid.
    /// </summary>
    /// <param name="keysArrayOrJwks">A JSON array of keys or a JWKS object.</param>
    /// <returns>The EC keys, in their order in the set.</returns>
    public static IReadOnlyList<EcJsonWebKey> Parse(JsonNode keysArrayOrJwks)
    {
        var keys = new List<EcJsonWebKey>();

        foreach (var json in ParseRaw(keysArrayOrJwks))
        {
            var key = EcJsonWebKey.FromJson(json);

            if (key is not null)
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    /// <summary>
    /// Reads every key object of a key set, of any key type (EC, OKP, RSA), without validating it.
    /// </summary>
    /// <param name="keysArrayOrJwks">A JSON array of keys or a JWKS object.</param>
    /// <returns>The key objects, in their order in the set.</returns>
    public static IReadOnlyList<JsonObject> ParseRaw(JsonNode keysArrayOrJwks)
    {
        var array = keysArrayOrJwks switch
        {
            JsonArray keysArray => keysArray,
            JsonObject jwks when jwks["keys"] is JsonArray keysArray => keysArray,
            _ => null,
        };

        var keys = new List<JsonObject>();

        if (array is null)
        {
            return keys;
        }

        foreach (var item in array)
        {
            if (item is JsonObject key)
            {
                keys.Add(key);
            }
        }

        return keys;
    }
}
