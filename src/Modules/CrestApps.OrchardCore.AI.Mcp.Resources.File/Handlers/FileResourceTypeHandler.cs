using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.Mcp.Resources.File.Handlers;

/// <summary>
/// Handles file:// URI resources by reading content from local files.
/// </summary>
public sealed class FileResourceTypeHandler : IMcpResourceTypeHandler
{
    private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();
    private readonly ILogger _logger;

    public FileResourceTypeHandler(ILogger<FileResourceTypeHandler> logger)
    {
        _logger = logger;
    }

    public string Type => FileResourceConstants.Type;

    public async Task<ReadResourceResult> ReadAsync(McpResource resource, CancellationToken cancellationToken = default)
    {
        var uri = resource.Resource?.Uri;

        if (string.IsNullOrEmpty(uri))
        {
            throw new InvalidOperationException("Resource URI is required.");
        }

        // Parse the file:// URI to get the file path
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var fileUri) || fileUri.Scheme != "file")
        {
            throw new InvalidOperationException($"Invalid file URI: {uri}. Expected format: file:///path/to/file");
        }

        var filePath = fileUri.LocalPath;

        if (!System.IO.File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        _logger.LogDebug("Reading file resource from: {FilePath}", filePath);

        // Determine MIME type
        var mimeType = resource.Resource?.MimeType;
        if (string.IsNullOrEmpty(mimeType))
        {
            if (!_contentTypeProvider.TryGetContentType(filePath, out mimeType))
            {
                mimeType = "application/octet-stream";
            }
        }

        // Read file content
        var content = await System.IO.File.ReadAllTextAsync(filePath, cancellationToken);

        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = uri,
                    MimeType = mimeType,
                    Text = content,
                }
            ]
        };
    }
}
