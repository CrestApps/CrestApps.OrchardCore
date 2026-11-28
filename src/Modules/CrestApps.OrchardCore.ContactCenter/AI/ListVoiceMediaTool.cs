using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Lists the clips of the voice media library, as the Voice media admin page lists them.
/// </summary>
internal sealed class ListVoiceMediaTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listVoiceMedia";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "Only clips whose name contains this text."
        },
        "missingProviderCopy": {
          "type": "boolean",
          "description": "Only clips that no telephony provider holds a copy of yet."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "Clips per page, 20 when not given, at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists voice media clips (greetings, prompts, hold audio) with format, whether a master copy exists and which providers hold a copy. Use it to find a clip id for a phone menu or queue treatment.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.ManageVoiceMedia;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var reader = new ContactCenterToolArguments(arguments);
        var (page, pageSize) = ReadPaging(reader);
        var name = reader.GetString("name");
        var missingProviderCopy = reader.GetBool("missingProviderCopy") == true;

        var clips = (await services.GetRequiredService<IVoiceMediaItemManager>().GetAllAsync(cancellationToken))
            .Where(clip => clip is not null)
            .Where(clip => name is null || (clip.Name?.Contains(name, StringComparison.OrdinalIgnoreCase) ?? false))
            .Where(clip => !missingProviderCopy || GetProviderNames(clip).Count == 0)
            .OrderBy(clip => clip.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var (items, total, truncated) = ContactCenterSetupLookups.Page(clips, page, pageSize);
        var localClock = services.GetService<ILocalClock>();
        var rows = new List<object>();

        foreach (var clip in items)
        {
            rows.Add(await DescribeAsync(clip, localClock));
        }

        return Json(new
        {
            items = rows,
            total,
            page,
            pageSize,
            truncated,
            canUpload = services.GetRequiredService<IVoiceMediaProvisioningService>().CanUpload,
        });
    }

    /// <summary>
    /// Describes a clip without its storage or provider references.
    /// </summary>
    internal static async Task<object> DescribeAsync(VoiceMediaItem clip, ILocalClock localClock)
    {
        var providers = GetProviderNames(clip);
        var hasMasterCopy = !string.IsNullOrWhiteSpace(clip.MasterStorageReference);

        return new
        {
            id = clip.ItemId,
            name = clip.Name,
            description = clip.Description,
            format = clip.Format,
            hasAudio = hasMasterCopy || providers.Count > 0,
            hasMasterCopy,
            providers,
            created = await ToLocalAsync(localClock, clip.CreatedUtc),
            modified = await ToLocalAsync(localClock, clip.ModifiedUtc),
        };
    }

    // The providers that hold a copy, as the clip's editor names them: the copies tracked per provider, or the single
    // copy recorded before copies were tracked per provider.
    private static List<string> GetProviderNames(VoiceMediaItem clip)
    {
        var providers = clip.ProviderReferences?.Keys.ToList() ?? [];

        if (providers.Count == 0 && !string.IsNullOrWhiteSpace(clip.ProviderName) && !string.IsNullOrWhiteSpace(clip.MediaReference))
        {
            providers.Add(clip.ProviderName);
        }

        return providers;
    }
}
