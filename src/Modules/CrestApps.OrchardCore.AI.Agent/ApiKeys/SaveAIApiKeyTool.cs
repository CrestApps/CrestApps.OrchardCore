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
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.ApiKeys;

/// <summary>
/// Enables or disables an existing API key, or changes when it expires, with the checks of its editor. It never makes
/// a key (a new key's value is shown once, on the page that makes it, and must not pass through the assistant), and it
/// never changes a key's name, scopes, roles, web sites or profiles: those widen what a key can do or reach, so a
/// person changes them on the key's page. Enabling a browser key, which is public, again is refused while one of its
/// profiles runs tools not safe for visitors.
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
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Enables or disables an existing API key, or sets or removes its expiry. It cannot create a key or change its name, scopes, roles, web sites or allowed profiles; it returns the API keys page link for those. A browser key whose profiles run tools not safe for visitors is not enabled again.";

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

            return $"Nothing was saved: this tool only enables, disables or changes the expiry of existing keys. A new key's value is shown once, to the person who makes it, and must not pass through the assistant. Make the key on the API keys page{(string.IsNullOrEmpty(createPath) ? "." : $": {createPath}")}";
        }

        var store = services.GetRequiredService<IAIApiKeyStore>();
        var key = await store.FindByIdAsync(apiKeyId.Trim(), cancellationToken);

        if (key is null)
        {
            return $"Nothing was saved: no API key has the ID '{apiKeyId.Trim()}'. Use listAIApiKeys to find it.";
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

        // The editor's rules are applied to a copy with the key's own name, scopes, roles, web sites and profiles, so
        // nothing changes on the stored key unless all of them pass.
        var changed = new AIApiKey { Kind = key.Kind };
        var errors = AIApiKeyValidator.Apply(changed, new AIApiKeyValues
        {
            Name = key.Name,
            Enabled = arguments.GetFirstValueOrDefault("enabled", key.Enabled),
            ExpiresUtc = expiresUtc,
            AllowedOrigins = key.AllowedOrigins,
            AllowedProfileIds = key.AllowedProfileIds,
            Scopes = key.Scopes,
            Roles = key.Roles,
        }, services.GetRequiredService<IStringLocalizer<SaveAIApiKeyTool>>());

        if (errors.Count > 0)
        {
            return $"Nothing was saved: {string.Join(" ", errors.Select(error => error.ErrorMessage))} Change the key on its page: {AIApiKeyToolSupport.GetEditPath(services, key.ItemId)}";
        }

        var refusal = await CheckBrowserReachAsync(services, key, changed, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var changes = new List<string>();

        if (key.Enabled != changed.Enabled)
        {
            changes.Add(changed.Enabled ? "enabled" : "disabled");
        }

        if (key.ExpiresUtc != changed.ExpiresUtc)
        {
            changes.Add("expiry");
        }

        if (changes.Count == 0)
        {
            return AgentToolResults.Json(new
            {
                id = key.ItemId,
                name = key.Name,
                changed = changes,
                note = "Nothing was different from the stored key, so nothing was saved. Only enabled and the expiry can be changed here; change anything else on the key's page.",
                editPath = AIApiKeyToolSupport.GetEditPath(services, key.ItemId),
            });
        }

        key.Enabled = changed.Enabled;
        key.ExpiresUtc = changed.ExpiresUtc;

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
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
            editPath = AIApiKeyToolSupport.GetEditPath(services, key.ItemId),
        });
    }

    private static async Task<string> CheckBrowserReachAsync(IServiceProvider services, AIApiKey before, AIApiKey after, CancellationToken cancellationToken)
    {
        if (after.Kind != AIApiKeyKind.Browser)
        {
            return null;
        }

        var utcNow = services.GetRequiredService<IClock>().UtcNow;

        // Only a key that starts to work again, because it is enabled or no longer expired, opens its profiles to every
        // visitor of its web sites; a key that already worked keeps what it reached.
        if (!AIApiKeyToolSupport.Works(after, utcNow) || AIApiKeyToolSupport.Works(before, utcNow))
        {
            return null;
        }

        var profileManager = services.GetRequiredService<IAIProfileManager>();
        var refusals = new List<string>();

        foreach (var profileId in after.AllowedProfileIds ?? [])
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
}
