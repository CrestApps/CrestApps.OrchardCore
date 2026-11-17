using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.Profiles;

/// <summary>
/// Restores an AI profile to a saved version, as the Restore button of the Versions page does; the restore is saved as
/// a new version. A version can bring back earlier tools, so the result is checked like a capability change first.
/// </summary>
public sealed class RestoreAIProfileVersionTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "restoreAIProfileVersion";

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
          "description": "The ID of the version to restore, from listAIProfileVersions."
        }
      },
      "required": ["profileId", "versionId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Restores a profile to a saved version; the restore is saved as a new version, so it can be undone. It can bring back earlier tools and instructions, so the same refusals as setAIProfileCapabilities apply.";

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
            return "Nothing was restored: profile versions are not kept on this site.";
        }

        arguments.TryGetFirstString("profileId", out var profileId);
        arguments.TryGetFirstString("versionId", out var versionId);

        var version = string.IsNullOrWhiteSpace(versionId) ? null : await versionStore.FindByIdAsync(versionId.Trim(), cancellationToken);
        var profileManager = services.GetRequiredService<IAIProfileManager>();
        var profile = version is null || string.IsNullOrWhiteSpace(profileId) || version.ProfileId != profileId.Trim()
            ? null
            : await profileManager.FindByIdAsync(version.ProfileId, cancellationToken);

        if (profile is null || version.Snapshot is null)
        {
            return $"Nothing was restored: the profile '{profileId}' has no version with the ID '{versionId}'. Use listAIProfileVersions to find it.";
        }

        var title = AIConfigurationGuard.GetTitle(profile);
        var versionsPath = AIProfileToolSupport.GetProfileVersionsPath(services, profile.ItemId);

        if ((await AIConfigurationGuard.GetProtectedAsync(services, cancellationToken)).IsProtected(profile))
        {
            return AIProfileToolSupport.RunningAssistantRefusal($"the profile '{title}'", versionsPath);
        }

        // Restore a copy first and check what it brings back; the stored profile is only touched once that passed.
        var copy = profile.Clone();
        AIProfileVersionRestorer.Restore(copy, version.Snapshot);

        var refusal = await AIConfigurationGuard.CheckCapabilityChangeAsync(services, profile, copy, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        AIProfileVersionRestorer.Restore(profile, version.Snapshot);
        AIProfileToolSupport.SetVersionNote(services, profile, $"Restored from version {version.Version}");

        await profileManager.UpdateAsync(profile, cancellationToken: cancellationToken);

        return AgentToolResults.Json(new
        {
            restored = true,
            id = profile.ItemId,
            title = AIConfigurationGuard.GetTitle(profile),
            restoredVersion = version.Version,
            newVersion = AIProfileToolSupport.GetVersion(profile),
            versionsPath,
        });
    }
}
