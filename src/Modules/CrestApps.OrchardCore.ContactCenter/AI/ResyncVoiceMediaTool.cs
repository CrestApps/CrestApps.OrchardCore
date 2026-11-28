using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Re-sends a voice media clip's master copy to every enabled telephony provider that hosts media, as the Re-sync media
/// button of the Voice media page does.
/// </summary>
internal sealed class ResyncVoiceMediaTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "resyncVoiceMedia";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id or name of the clip to re-sync."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Re-sends a clip's master copy to every enabled telephony provider and reports per provider whether it was verified, uploaded or refused. Use it after changing provider or when a prompt does not play.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.ManageVoiceMedia;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var reader = new ContactCenterToolArguments(arguments);
        var id = reader.GetString("id");

        if (id is null)
        {
            return "Nothing was re-synced: give the id or name of the clip.";
        }

        var clip = await ContactCenterSetupLookups.FindAsync(services.GetRequiredService<IVoiceMediaItemManager>(), id, record => record.Name, cancellationToken);

        if (clip is null)
        {
            return $"Nothing was re-synced: no voice media clip has the id or name '{id}'. Use listVoiceMedia to find it.";
        }

        var result = await services.GetRequiredService<IVoiceMediaProvisioningService>().ResyncAsync(clip, cancellationToken);

        string message = null;

        if (!result.HasMasterCopy)
        {
            message = $"'{clip.Name}' has no master copy, so it cannot be re-synced. Upload its audio again (saveVoiceMedia with a mediaPath) to protect it.";
        }
        else if (result.Providers.Count == 0)
        {
            message = "No enabled telephony provider can host media, so there was nothing to re-sync.";
        }

        return Json(new
        {
            id = clip.ItemId,
            name = clip.Name,
            hasMasterCopy = result.HasMasterCopy,
            message,
            providers = result.Providers
                .Select(provider => new
                {
                    provider = provider.Key,
                    outcome = provider.Value switch
                    {
                        VoiceMediaResyncOutcome.Verified => "verified",
                        VoiceMediaResyncOutcome.Uploaded => "uploaded",
                        _ => "refused",
                    },
                    detail = provider.Value switch
                    {
                        VoiceMediaResyncOutcome.Verified => $"{provider.Key} already holds the clip.",
                        VoiceMediaResyncOutcome.Uploaded => $"The clip was sent to {provider.Key}.",
                        _ => $"{provider.Key} did not accept the clip. Check the provider's configuration and try again.",
                    },
                })
                .ToList(),
        });
    }
}
