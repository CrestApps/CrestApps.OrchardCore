using System.Globalization;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.AI.Agent.ApiKeys;

/// <summary>
/// Updates an existing API key with the checks of its editor. It never makes a key: a new key's value is shown once,
/// on the page that makes it, and must not pass through the assistant. A change that lets a browser key, which is
/// public, chat with a profile that runs tools not safe for visitors is refused.
/// </summary>
public sealed class SaveAIApiKeyTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveAIApiKey";

    private static readonly string[] _localFormats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"];

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "apiKeyId": {
          "type": "string",
          "description": "The ID of the key to change, from listAIApiKeys. Without it the tool only returns the page where a person makes a new key."
        },
        "name": {
          "type": "string",
          "description": "The new name of the key."
        },
        "enabled": {
          "type": "boolean",
          "description": "Whether the key works. false disables it at once; clients using it stop working."
        },
        "expiresOn": {
          "type": "string",
          "description": "When the key stops working, as a local date (yyyy-MM-dd) or a local date and time (yyyy-MM-ddTHH:mm), without a time zone."
        },
        "removeExpiry": {
          "type": "boolean",
          "description": "true makes the key work until it is disabled or deleted. Do not give it with expiresOn."
        },
        "scopes": {
          "type": "array",
          "items": { "type": "string", "enum": ["profiles", "conversations", "tests", "calls", "numbers", "knowledge", "contacts", "chat", "recordings"] },
          "description": "Server keys only: the areas of the AI API the key may call. Replaces the current list."
        },
        "roles": {
          "type": "array",
          "items": { "type": "string" },
          "description": "Server keys only: the roles whose permissions the key acts with. Replaces the current list. Adding a role gives every client of the key that role's permissions, so confirm it with the user."
        },
        "allowedOrigins": {
          "type": "array",
          "items": { "type": "string" },
          "description": "Browser keys only: the web sites the key may be used from, each as its origin, such as https://www.example.com. Replaces the current list."
        },
        "allowedProfileIds": {
          "type": "array",
          "items": { "type": "string" },
          "description": "Browser keys only: the IDs of the AI profiles the key may chat with, from listAIProfiles. Replaces the current list. Visitors of the allowed web sites can chat with these profiles without signing in."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Updates an existing API key: enable or disable it, or change its name, scopes, roles, origins, allowed profiles or expiry. It cannot create a key, because a new key's value is shown once and must not pass through the assistant; it returns the API keys page link for that. A browser key may not be given a profile that runs tools not safe for visitors.";

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

        if (!arguments.TryGetFirstString("apiKeyId", out var apiKeyId) || string.IsNullOrWhiteSpace(apiKeyId))
        {
            var createPath = AIApiKeyToolSupport.GetCreatePath(services);

            return $"Nothing was saved: this tool only changes existing keys. A new key's value is shown once, to the person who makes it, and must not pass through the assistant. Make the key on the API keys page{(string.IsNullOrEmpty(createPath) ? "." : $": {createPath}")}";
        }

        var store = services.GetRequiredService<IAIApiKeyStore>();
        var key = await store.FindByIdAsync(apiKeyId.Trim(), cancellationToken);

        if (key is null)
        {
            return $"Nothing was saved: no API key has the ID '{apiKeyId.Trim()}'. Use listAIApiKeys to find it.";
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "scopes", out var scopes) ||
            !AgentToolArguments.TryReadStrings(arguments, "roles", out var roles) ||
            !AgentToolArguments.TryReadStrings(arguments, "allowedOrigins", out var origins) ||
            !AgentToolArguments.TryReadStrings(arguments, "allowedProfileIds", out var profileIds))
        {
            return "Nothing was saved: scopes, roles, allowedOrigins and allowedProfileIds must each be a list of texts.";
        }

        if (key.Kind == AIApiKeyKind.Browser && (scopes is not null || roles is not null))
        {
            return "Nothing was saved: a browser key always has the chat scope and no roles. Give allowedOrigins or allowedProfileIds instead.";
        }

        if (key.Kind == AIApiKeyKind.Server && (origins is not null || profileIds is not null))
        {
            return "Nothing was saved: a server key has no web sites or profiles of its own; it acts with the permissions of its roles. Give scopes or roles instead.";
        }

        var hasExpiresOn = arguments.TryGetFirstString("expiresOn", out var expiresOn) && !string.IsNullOrWhiteSpace(expiresOn);
        var removeExpiry = arguments.GetFirstValueOrDefault("removeExpiry", false);
        var expiresUtc = key.ExpiresUtc;

        if (hasExpiresOn && removeExpiry)
        {
            return "Nothing was saved: give expiresOn or removeExpiry, not both.";
        }

        if (hasExpiresOn)
        {
            if (!DateTime.TryParseExact(expiresOn.Trim(), _localFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            {
                return "Nothing was saved: expiresOn must be a local date as yyyy-MM-dd, or a local date and time as yyyy-MM-ddTHH:mm, without a time zone.";
            }

            expiresUtc = DateTime.SpecifyKind(await services.GetRequiredService<ILocalClock>().ConvertToUtcAsync(local), DateTimeKind.Utc);
        }
        else if (removeExpiry)
        {
            expiresUtc = null;
        }

        if (roles is not null)
        {
            var known = (await services.GetRequiredService<IRoleService>().GetRoleNamesAsync())
                .Where(role => !string.Equals(role, OrchardCoreConstants.Roles.Anonymous, StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unknown = roles.Where(role => !known.Contains(role)).ToList();

            if (unknown.Count > 0)
            {
                return $"Nothing was saved: {string.Join(", ", unknown.Select(role => $"'{role}'"))} {(unknown.Count == 1 ? "is not a role" : "are not roles")} a key can act with. The Anonymous role cannot be given to a key.";
            }
        }

        var profileManager = services.GetRequiredService<IAIProfileManager>();

        if (profileIds is not null)
        {
            var missing = new List<string>();

            foreach (var profileId in profileIds)
            {
                if (await profileManager.FindByIdAsync(profileId, cancellationToken) is null)
                {
                    missing.Add(profileId);
                }
            }

            if (missing.Count > 0)
            {
                return $"Nothing was saved: no AI profile has the ID {string.Join(", ", missing.Select(id => $"'{id}'"))}. Use listAIProfiles to find the IDs.";
            }
        }

        if (scopes is not null)
        {
            var unknown = scopes.Where(scope => !AIApiKeys.Scopes.All.Contains(scope)).ToList();

            if (unknown.Count > 0)
            {
                return $"Nothing was saved: {string.Join(", ", unknown.Select(scope => $"'{scope}'"))} {(unknown.Count == 1 ? "is not a scope" : "are not scopes")}. The scopes are {string.Join(", ", AIApiKeys.Scopes.All.Order(StringComparer.Ordinal))}.";
            }
        }

        arguments.TryGetFirstString("name", out var name);

        // The editor's rules are applied to a copy, so nothing changes on the stored key unless all of them pass.
        var changed = new AIApiKey { Kind = key.Kind };
        var errors = AIApiKeyValidator.Apply(changed, new AIApiKeyValues
        {
            Name = name ?? key.Name,
            Enabled = arguments.GetFirstValueOrDefault("enabled", key.Enabled),
            ExpiresUtc = expiresUtc,
            AllowedOrigins = origins ?? key.AllowedOrigins,
            AllowedProfileIds = profileIds ?? key.AllowedProfileIds,
            Scopes = scopes ?? key.Scopes,
            Roles = roles ?? key.Roles,
        }, services.GetRequiredService<IStringLocalizer<SaveAIApiKeyTool>>());

        if (errors.Count > 0)
        {
            return $"Nothing was saved: {string.Join(" ", errors.Select(error => error.ErrorMessage))}";
        }

        var refusal = await CheckBrowserReachAsync(services, profileManager, key, changed, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var changes = Describe(key, changed);

        if (changes.Count == 0)
        {
            return AgentToolResults.Json(new
            {
                id = key.ItemId,
                name = key.Name,
                changed = changes,
                note = "Nothing was different from the stored key, so nothing was saved.",
            });
        }

        key.Name = changed.Name;
        key.Enabled = changed.Enabled;
        key.ExpiresUtc = changed.ExpiresUtc;
        key.Scopes = changed.Scopes;
        key.Roles = changed.Roles;
        key.AllowedOrigins = changed.AllowedOrigins;
        key.AllowedProfileIds = changed.AllowedProfileIds;

        await store.UpdateAsync(key, cancellationToken);

        var localClock = services.GetRequiredService<ILocalClock>();

        return AgentToolResults.Json(new
        {
            id = key.ItemId,
            name = key.Name,
            kind = key.Kind.ToString(),
            changed = changes,
            enabled = key.Enabled,
            expires = await AgentToolResults.ToLocalAsync(localClock, key.ExpiresUtc),
            scopes = key.Scopes,
            roles = key.Roles,
            allowedOrigins = key.AllowedOrigins,
            allowedProfileIds = key.AllowedProfileIds,
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
            editPath = AIApiKeyToolSupport.GetEditPath(services, key.ItemId),
        });
    }

    private static async Task<string> CheckBrowserReachAsync(IServiceProvider services, IAIProfileManager profileManager, AIApiKey before, AIApiKey after, CancellationToken cancellationToken)
    {
        if (after.Kind != AIApiKeyKind.Browser)
        {
            return null;
        }

        var utcNow = services.GetRequiredService<IClock>().UtcNow;

        if (!AIApiKeyToolSupport.Works(after, utcNow))
        {
            return null;
        }

        // A profile the key already reached keeps what it had; a profile the key starts to reach, because it is added,
        // or because the key is enabled again or no longer expired, is opened to every visitor of the allowed sites.
        var reachedBefore = AIApiKeyToolSupport.Works(before, utcNow)
            ? (before.AllowedProfileIds ?? []).ToHashSet(StringComparer.Ordinal)
            : [];

        var refusals = new List<string>();

        foreach (var profileId in after.AllowedProfileIds.Where(id => !reachedBefore.Contains(id)))
        {
            var profile = await profileManager.FindByIdAsync(profileId, cancellationToken);

            if (profile is null)
            {
                continue;
            }

            var unsafeItems = await AIConfigurationGuard.GetUnsafeCapabilitiesAsync(services, profile, cancellationToken);

            if (unsafeItems.Count > 0)
            {
                refusals.Add($"the profile '{AIConfigurationGuard.GetTitle(profile)}' can run {AIConfigurationGuard.Join(unsafeItems)}");
            }
        }

        if (refusals.Count == 0)
        {
            return null;
        }

        return $"Nothing was saved: a browser key is public, so every visitor of its web sites could chat with the profiles it names, and {string.Join("; ", refusals)}, which {(refusals.Count == 1 ? "is" : "are")} not marked safe for visitors who are not signed in. Remove those tools from the profile first, or change the key on its admin page if you are sure.";
    }

    private static List<string> Describe(AIApiKey before, AIApiKey after)
    {
        var changes = new List<string>();

        if (!string.Equals(before.Name, after.Name, StringComparison.Ordinal))
        {
            changes.Add("name");
        }

        if (before.Enabled != after.Enabled)
        {
            changes.Add(after.Enabled ? "enabled" : "disabled");
        }

        if (before.ExpiresUtc != after.ExpiresUtc)
        {
            changes.Add("expiry");
        }

        if (!SameItems(before.Scopes, after.Scopes))
        {
            changes.Add("scopes");
        }

        if (!SameItems(before.Roles, after.Roles))
        {
            changes.Add("roles");
        }

        if (!SameItems(before.AllowedOrigins, after.AllowedOrigins))
        {
            changes.Add("allowedOrigins");
        }

        if (!SameItems(before.AllowedProfileIds, after.AllowedProfileIds))
        {
            changes.Add("allowedProfileIds");
        }

        return changes;
    }

    private static bool SameItems(List<string> before, List<string> after)
        => (before ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(after ?? []);
}
