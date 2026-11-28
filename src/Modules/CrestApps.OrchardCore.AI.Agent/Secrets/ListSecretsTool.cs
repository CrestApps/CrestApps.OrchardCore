using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Secrets;

namespace CrestApps.OrchardCore.AI.Agent.Secrets;

/// <summary>
/// Lists the secrets of the site from the information the secret stores give about them, as the Secrets admin page
/// does: filtered by text, type and expiry, ordered by name and paged. It never reads the value of a secret.
/// </summary>
public sealed class ListSecretsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listSecrets";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Text the name, description, store or type of a secret must contain. Leave it out to list every secret."
        },
        "type": {
          "type": "string",
          "enum": [
            "TextSecret",
            "RsaKeySecret",
            "X509Secret"
          ],
          "description": "List the secrets of one type only: a text value, an RSA key pair or an X.509 certificate."
        },
        "expiry": {
          "type": "string",
          "enum": [
            "expired",
            "expiringSoon",
            "neverExpires"
          ],
          "description": "List only the secrets that are past their expiry date, that expire within 30 days, or that have no expiry date."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1. The default is 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of secrets on a page. The default is 20 and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists secrets by name, store, type, description and created, updated and expiry dates, with optional search, type and expiry filters, plus the available secret stores. It never returns a secret value; use it to find a secret name to reference in a settings page or to spot expiring secrets.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.ViewSecrets))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("search", out var search);
        arguments.TryGetFirstString("type", out var type);
        arguments.TryGetFirstString("expiry", out var expiry);

        search = search?.Trim();
        type = type?.Trim();
        expiry = expiry?.Trim().ToLowerInvariant();

        if (expiry is not (null or "expired" or "expiringsoon" or "neverexpires"))
        {
            return "Nothing was listed: expiry must be expired, expiringSoon or neverExpires.";
        }

        var manager = services.GetRequiredService<ISecretManager>();
        var utcNow = services.GetRequiredService<IClock>().UtcNow;
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        // Only the information about the secrets is read. The values stay in their stores.
        var all = (await manager.GetSecretInfosAsync()).ToList();

        var filtered = all
            .Where(info => string.IsNullOrEmpty(search) || Matches(info, search))
            .Where(info => string.IsNullOrEmpty(type) || string.Equals(SecretToolSupport.GetTypeName(info.Type), type, StringComparison.OrdinalIgnoreCase))
            .Where(info => expiry switch
            {
                "expired" => SecretToolSupport.IsExpired(info, utcNow),
                "expiringsoon" => SecretToolSupport.IsExpiringSoon(info, utcNow),
                "neverexpires" => !info.ExpiresUtc.HasValue,
                _ => true,
            })
            .OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(info => info.Store, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var items = new List<object>();

        foreach (var info in filtered.Skip((page - 1) * pageSize).Take(pageSize))
        {
            items.Add(await SecretToolSupport.DescribeAsync(services, info));
        }

        return AgentToolResults.Json(new
        {
            items,
            total = filtered.Count,
            page,
            pageSize,
            truncated = filtered.Count > page * pageSize
                ? true
                : (bool?)null,
            expiredCount = all.Count(info => SecretToolSupport.IsExpired(info, utcNow)),
            expiringWithin30DaysCount = all.Count(info => SecretToolSupport.IsExpiringSoon(info, utcNow)),
            stores = manager.GetStores()
                .Select(store => new
                {
                    name = store.Name,
                    readOnly = store.IsReadOnly,
                })
                .ToList(),
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(services.GetRequiredService<ILocalClock>()),
            note = "The value of a secret is never returned. A setting references a secret by its name.",
        });
    }

    private static bool Matches(SecretInfo info, string search)
    {
        return Contains(info.Name, search) ||
            Contains(info.Description, search) ||
            Contains(info.Store, search) ||
            Contains(SecretToolSupport.GetTypeName(info.Type), search);
    }

    private static bool Contains(string value, string search)
        => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
}
