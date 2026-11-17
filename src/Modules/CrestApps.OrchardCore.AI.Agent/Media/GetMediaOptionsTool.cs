using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.AI.Agent.Media;

/// <summary>
/// Returns the media configuration the read-only Media Options page shows, the media features that are enabled, and
/// the storage the media store still permits.
/// </summary>
public sealed class GetMediaOptionsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getMediaOptions";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {},
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns the media configuration: allowed and restricted file extensions, maximum upload size, supported image sizes, CDN base URL, which storage and media features are enabled and the remaining permitted storage. Call it before importing a file to know whether its extension and size will be accepted.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, MediaPermissions.ViewMediaOptions))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var mediaOptions = services.GetRequiredService<IOptions<MediaOptions>>().Value;
        var mediaFileStore = services.GetRequiredService<IMediaFileStore>();
        var enabledFeatures = await services.GetRequiredService<IShellFeaturesManager>().GetEnabledFeaturesAsync();

        long? permittedStorageBytes = null;
        string permittedStorageNote;

        // The remaining storage is what the Media Library shows, so it has the check of the Media Library.
        if (await MediaToolSupport.CanManageMediaAsync(services) && await MediaToolSupport.CanManagePathAsync(services, string.Empty))
        {
            permittedStorageBytes = await mediaFileStore.GetPermittedStorageAsync();
            permittedStorageNote = permittedStorageBytes is null
                ? "The media store reports no storage limit."
                : null;
        }
        else
        {
            permittedStorageNote = "The remaining storage is shown to users who may manage the media library.";
        }

        return AgentToolResults.Json(new
        {
            allowedFileExtensions = Sort(mediaOptions.AllowedFileExtensions),
            restrictedFileExtensions = Sort(mediaOptions.RestrictedFileExtensions),
            canUploadRestrictedMedia = await MediaToolSupport.CanUploadRestrictedMediaAsync(services),
            maxFileSizeBytes = mediaOptions.MaxFileSize,
            maxUploadChunkSizeBytes = mediaOptions.MaxUploadChunkSize,
            supportedSizes = mediaOptions.SupportedSizes ?? [],
            useTokenizedQueryString = mediaOptions.UseTokenizedQueryString,
            cdnBaseUrl = mediaOptions.CdnBaseUrl,
            assetsRequestPath = mediaOptions.AssetsRequestPath.Value,
            assetsPath = mediaOptions.AssetsPath,
            assetsUsersFolder = mediaOptions.AssetsUsersFolder,
            maxBrowserCacheDays = mediaOptions.MaxBrowserCacheDays,
            maxSecureFilesBrowserCacheDays = mediaOptions.MaxSecureFilesBrowserCacheDays,
            maxCacheDays = mediaOptions.MaxCacheDays,
            temporaryFileLifetimeMinutes = mediaOptions.TemporaryFileLifetime.TotalMinutes,
            thumbnailsDisabled = mediaOptions.DisableThumbnails,
            storage = new
            {
                name = mediaFileStore.StorageName,
                hasHierarchicalNamespace = mediaFileStore.Capabilities?.HasHierarchicalNamespace,
                supportsAtomicMove = mediaFileStore.Capabilities?.SupportsAtomicMove,
            },
            enabledMediaFeatures = enabledFeatures
                .Select(feature => feature.Id)
                .Where(id => id.StartsWith(OrchardCoreFeatureIds.Media, StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase),
            permittedStorageBytes,
            permittedStorageNote,
            note = "Media is configured in the site's configuration files, so these options cannot be changed from here.",
        });
    }

    private static IEnumerable<string> Sort(IEnumerable<string> extensions)
        => (extensions ?? []).Order(StringComparer.OrdinalIgnoreCase);
}
