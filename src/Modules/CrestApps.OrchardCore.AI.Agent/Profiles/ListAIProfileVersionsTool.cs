using System.Text.Json;
using System.Text.RegularExpressions;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Profiles;

/// <summary>
/// Lists the saved versions of an AI profile, as its Versions page does, and with a version ID compares it with the
/// profile as it is now or with another version, as the Compare page does. Settings the compare does not know are left
/// out, and a setting whose name looks like a credential is never shown.
/// </summary>
public sealed partial class ListAIProfileVersionsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listAIProfileVersions";

    private const int MaxValueLength = 2000;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "profileId": {
          "type": "string",
          "description": "The ID of the profile, from listAIProfiles."
        },
        "versionId": {
          "type": "string",
          "description": "The ID of a version from this list. When given, the result also has the differences between that version and the profile as it is now."
        },
        "compareWithVersionId": {
          "type": "string",
          "description": "With versionId: the ID of another version of the same profile to compare with, instead of the profile as it is now."
        },
        "includeUnchanged": {
          "type": "boolean",
          "description": "With versionId: also list the settings that are the same in both. Only changed settings are listed when left out."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page of versions to return, starting at 1, newest first."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many versions a page holds, 20 when left out and at most 50."
        }
      },
      "required": ["profileId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists a profile's saved versions (number, date, author, note). With versionId it also returns the field-by-field differences against the current profile or another version.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIProfiles))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var versionStore = services.GetService<IAIProfileVersionStore>();

        if (versionStore is null)
        {
            return "Profile versions are not kept on this site.";
        }

        arguments.TryGetFirstString("profileId", out var profileId);
        arguments.TryGetFirstString("versionId", out var versionId);
        arguments.TryGetFirstString("compareWithVersionId", out var compareWithVersionId);
        var includeUnchanged = arguments.GetFirstValueOrDefault("includeUnchanged", false);
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        var profileManager = services.GetRequiredService<IAIProfileManager>();
        var profile = string.IsNullOrWhiteSpace(profileId) ? null : await profileManager.FindByIdAsync(profileId.Trim(), cancellationToken);

        if (profile is null)
        {
            return $"No AI profile has the ID '{profileId}'. Use listAIProfiles to find the ID.";
        }

        var localClock = services.GetRequiredService<ILocalClock>();
        var current = AIProfileToolSupport.GetVersion(profile);
        var (versions, count) = await versionStore.PageAsync(profile.ItemId, page, pageSize, cancellationToken);
        var items = new List<object>();

        foreach (var version in versions)
        {
            items.Add(new
            {
                id = version.ItemId,
                version = version.Version,
                created = await AgentToolResults.ToLocalAsync(localClock, version.CreatedUtc),
                author = version.Author,
                note = version.Note,
                isCurrent = version.Version == current,
            });
        }

        object comparison = null;

        if (!string.IsNullOrWhiteSpace(versionId))
        {
            var version = await versionStore.FindByIdAsync(versionId.Trim(), cancellationToken);

            if (version is null || version.ProfileId != profile.ItemId || version.Snapshot is null)
            {
                return $"The profile '{AIConfigurationGuard.GetTitle(profile)}' has no version with the ID '{versionId}'. Use this tool without versionId to list them.";
            }

            var other = string.IsNullOrWhiteSpace(compareWithVersionId) ? null : await versionStore.FindByIdAsync(compareWithVersionId.Trim(), cancellationToken);

            if (!string.IsNullOrWhiteSpace(compareWithVersionId) && (other is null || other.ProfileId != profile.ItemId || other.Snapshot is null))
            {
                return $"The profile '{AIConfigurationGuard.GetTitle(profile)}' has no version with the ID '{compareWithVersionId}'.";
            }

            var rows = AIProfileVersionComparer.Compare(version.Snapshot, other?.Snapshot ?? profile)
                .Where(row => includeUnchanged || row.Changed)
                .Where(row => row.Section != AIProfileVersionComparer.Other)
                .Select(row => new
                {
                    section = row.Section,
                    setting = row.Label,
                    then = Show(row.Label, row.Then),
                    now = Show(row.Label, row.Now),
                    changed = row.Changed,
                })
                .ToList();

            comparison = new
            {
                versionId = version.ItemId,
                version = version.Version,
                comparedWith = other is null ? "the profile as it is now" : $"version {other.Version}",
                rows,
                note = "Settings the compare does not group into a section are not listed, and credential-like values are hidden.",
            };
        }

        return AgentToolResults.Json(new
        {
            profileId = profile.ItemId,
            title = AIConfigurationGuard.GetTitle(profile),
            currentVersion = current,
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
            items,
            total = count,
            page,
            pageSize,
            truncated = (page * pageSize) < count,
            comparison,
            versionsPath = AIProfileToolSupport.GetProfileVersionsPath(services, profile.ItemId),
        });
    }

    /// <summary>
    /// Hides a value whose setting name looks like a credential and shortens a long one.
    /// </summary>
    /// <param name="label">The name of the setting.</param>
    /// <param name="value">The value.</param>
    /// <returns>The value to show.</returns>
    internal static string Show(string label, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (label is not null && CredentialName().IsMatch(label))
        {
            return "(hidden)";
        }

        return value.Length > MaxValueLength ? value[..MaxValueLength] + "…" : value;
    }

    [GeneratedRegex(@"api\s*key|secret|password|access\s*token|refresh\s*token|bearer|credential|private\s*key|connection\s*string|salt|hash|certificate", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CredentialName();
}
