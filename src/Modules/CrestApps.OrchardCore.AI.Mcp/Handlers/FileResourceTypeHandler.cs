using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

/// <summary>
/// Handles file:// URI resources by reading content from local files.
/// </summary>
public sealed class FileResourceTypeHandler : McpResourceTypeHandlerBase
{
    public const string TypeName = "file";

    private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();
    private readonly ILogger _logger;

    public FileResourceTypeHandler(ILogger<FileResourceTypeHandler> logger)
        : base(TypeName)
    {
        _logger = logger;
    }

    protected override async Task<ReadResourceResult> GetResultAsync(McpResource resource, McpResourceUri resourceUri, CancellationToken cancellationToken)
    {
        var filePath = resourceUri.Path;
        var hasPath = !string.IsNullOrEmpty(filePath);

        if (!hasPath || !File.Exists(filePath) || !filePath.StartsWith("filesystem/", StringComparison.OrdinalIgnoreCase))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("File not found for resource URI: {ResourceUri}", resourceUri.Uri);
            }

            if (!hasPath)
            {
                return CreateErrorResult(resource.Resource.Uri, "File not found");
            }

            return CreateErrorResult(resource.Resource.Uri, $"File not found: {filePath}");
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Reading file resource from: {FilePath}", filePath);
        }

        // Determine MIME type
        var mimeType = resource.Resource?.MimeType;
        if (string.IsNullOrEmpty(mimeType))
        {
            if (!_contentTypeProvider.TryGetContentType(filePath, out mimeType))
            {
                mimeType = "application/octet-stream";
            }
        }

        if (IsTextMimeType(mimeType))
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);

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

        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);

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
