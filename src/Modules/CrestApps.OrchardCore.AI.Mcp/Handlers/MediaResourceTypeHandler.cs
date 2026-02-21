using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

/// <summary>
/// Handles media resources by reading content from Orchard Core's media store.
/// Supported variable: {path} - the media path to read.
/// </summary>
public sealed class MediaResourceTypeHandler : McpResourceTypeHandlerBase
{
    public const string TypeName = "media";

    private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();
    private readonly IMediaFileStore _mediaFileStore;
    private readonly ILogger _logger;

    public MediaResourceTypeHandler(
        IMediaFileStore mediaFileStore,
        ILogger<MediaResourceTypeHandler> logger)
        : base(TypeName)
    {
        _mediaFileStore = mediaFileStore;
        _logger = logger;
    }

    protected override async Task<ReadResourceResult> GetResultAsync(McpResource resource, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        variables.TryGetValue("path", out var mediaPath);

        if (string.IsNullOrEmpty(mediaPath))
        {
            return CreateErrorResult(resource.Resource.Uri, $"Media URI '{resource.Resource.Uri}' does not contain a valid path.");
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Reading media resource from: {MediaPath}", mediaPath);
        }

        var fileInfo = await _mediaFileStore.GetFileInfoAsync(mediaPath);

        if (fileInfo is null)
        {
            return CreateErrorResult(resource.Resource.Uri, $"Media file not found: {mediaPath}");
        }

        // Determine MIME type.
        var mimeType = resource.Resource?.MimeType;
        if (string.IsNullOrEmpty(mimeType))
        {
            if (!_contentTypeProvider.TryGetContentType(mediaPath, out mimeType))
            {
                mimeType = "application/octet-stream";
            }
        }

        // Read file content from the media store.
        using var stream = await _mediaFileStore.GetFileStreamAsync(mediaPath);

        if (IsTextMimeType(mimeType))
        {
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync(cancellationToken);

            return new ReadResourceResult
            {
                Contents =
                [
                    new TextResourceContents
                    {
                        Uri = resource.Resource.Uri,
                        MimeType = mimeType,
                        Text = content,
                    }
                ]
            };
        }

        var bytes = new byte[fileInfo.Length];
        await stream.ReadExactlyAsync(bytes, cancellationToken);

        return new ReadResourceResult
        {
            Contents =
            [
                new BlobResourceContents
                {
                    Uri = resource.Resource.Uri,
                    MimeType = mimeType,
                    Blob = Convert.ToBase64String(bytes),
                }
            ]
        };
    }
}
