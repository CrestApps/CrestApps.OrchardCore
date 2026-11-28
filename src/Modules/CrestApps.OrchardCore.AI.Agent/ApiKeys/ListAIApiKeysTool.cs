using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.ApiKeys;

/// <summary>
/// Lists API keys, as the API keys page does. A key's value and its hash are never returned: a server key's value is
/// shown once when it is made, and a browser key's value is only shown on its own editor.
/// </summary>
public sealed class ListAIApiKeysTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listAIApiKeys";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Only keys whose name contains this text."
        },
        "kind": {
          "type": "string",
          "enum": ["Server", "Browser"],
          "description": "Only keys of this kind: Server keys call the AI API with the permissions of their roles; Browser keys let web pages chat with the profiles they name."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many keys per page, at most 50. Defaults to 20."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists API keys with name, kind, prefix, scopes, roles, allowed origins and profiles, expiry, enabled state and last use, newest first. The key value and its hash are never returned. The result also links to the page where a new key is made.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIApiKeys))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("search", out var search);
        arguments.TryGetFirstString("kind", out var kindText);
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        AIApiKeyKind? kind = null;

        if (!string.IsNullOrWhiteSpace(kindText))
        {
            if (!Enum.TryParse<AIApiKeyKind>(kindText.Trim(), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                return "Nothing was listed: kind must be Server or Browser.";
            }

            kind = parsed;
        }

        IEnumerable<AIApiKey> keys = await services.GetRequiredService<IAIApiKeyStore>().GetAllAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            keys = keys.Where(key => key.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) == true);
        }

        if (kind is not null)
        {
            keys = keys.Where(key => key.Kind == kind);
        }

        var filtered = keys
            .OrderByDescending(key => key.CreatedUtc)
            .ThenBy(key => key.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var localClock = services.GetRequiredService<ILocalClock>();
        var utcNow = services.GetRequiredService<IClock>().UtcNow;
        var profileTitles = (await services.GetRequiredService<IAIProfileManager>().GetAllAsync(cancellationToken))
            .Where(profile => !string.IsNullOrEmpty(profile.ItemId))
            .ToDictionary(profile => profile.ItemId, AIConfigurationGuard.GetTitle, StringComparer.Ordinal);

        var items = new List<object>();

        foreach (var key in filtered.Skip((page - 1) * pageSize).Take(pageSize))
        {
            items.Add(new
            {
                id = key.ItemId,
                name = key.Name,
                kind = key.Kind.ToString(),
                prefix = key.Prefix,
                scopes = key.Scopes ?? [],
                roles = key.Roles ?? [],
                allowedOrigins = key.AllowedOrigins ?? [],
                allowedProfiles = (key.AllowedProfileIds ?? [])
                    .Select(id => new { id, title = profileTitles.GetValueOrDefault(id), exists = profileTitles.ContainsKey(id) })
                    .ToList(),
                enabled = key.Enabled,
                expires = await AgentToolResults.ToLocalAsync(localClock, key.ExpiresUtc),
                expired = key.ExpiresUtc is { } expiresUtc && expiresUtc <= utcNow,
                works = AIApiKeyToolSupport.Works(key, utcNow),
                created = await AgentToolResults.ToLocalAsync(localClock, key.CreatedUtc == default ? null : key.CreatedUtc),
                createdBy = key.Author,
                lastUsed = await AgentToolResults.ToLocalAsync(localClock, key.LastUsedUtc),
                neverUsed = key.LastUsedUtc is null,
                editPath = AIApiKeyToolSupport.GetEditPath(services, key.ItemId),
            });
        }

        return AgentToolResults.Json(new
        {
            items,
            total = filtered.Count,
            page,
            pageSize,
            truncated = filtered.Count > page * pageSize,
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
            createPath = AIApiKeyToolSupport.GetCreatePath(services),
            note = "A new key is made on the createPath page, where its value is shown once. The assistant never sees or passes on a key's value.",
        });
    }
}
