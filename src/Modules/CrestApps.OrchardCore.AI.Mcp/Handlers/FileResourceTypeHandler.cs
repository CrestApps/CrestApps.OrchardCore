using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

/// <summary>
/// Handles file resources by reading content from an <see cref="IFileProvider"/> resolved by name.
/// Supported variables:
///   {providerName} - the file provider name to resolve.
///   {fileName} - the file path within the provider.
/// </summary>
public sealed class FileResourceTypeHandler : McpResourceTypeHandlerBase
{
    public const string TypeName = "file";

    private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();
    private readonly IMcpFileProviderResolver _fileProviderResolver;
    private readonly ILogger _logger;

    public FileResourceTypeHandler(
        IMcpFileProviderResolver fileProviderResolver,
        ILogger<FileResourceTypeHandler> logger)
        : base(TypeName)
    {
        _fileProviderResolver = fileProviderResolver;
        _logger = logger;
    }

    protected override async Task<ReadResourceResult> GetResultAsync(McpResource resource, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        variables.TryGetValue("providerName", out var providerName);
        variables.TryGetValue("fileName", out var fileName);

        if (string.IsNullOrEmpty(fileName))
        {
            return CreateErrorResult(resource.Resource.Uri, "File name is required. Include {fileName} in the URI pattern.");
        }

        var fileProvider = _fileProviderResolver.Resolve(providerName);

        if (fileProvider is null)
        {
            return CreateErrorResult(resource.Resource.Uri, $"File provider not found: '{providerName}'.");
        }

        var fileInfo = fileProvider.GetFileInfo(fileName);

        if (fileInfo is null || !fileInfo.Exists)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("File not found for resource URI: {ResourceUri}, provider: {ProviderName}, file: {FileName}", resource.Resource?.Uri, providerName, fileName);
            }

            return CreateErrorResult(resource.Resource.Uri, $"File not found: {fileName}");
        }

        if (fileInfo.IsDirectory)
        {
            return CreateErrorResult(resource.Resource.Uri, $"The path '{fileName}' is a directory, not a file. Only file resources are supported.");
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Reading file resource from provider '{ProviderName}': {FileName}", providerName, fileName);
        }

        // Determine MIME type.
        var mimeType = resource.Resource?.MimeType;

        if (string.IsNullOrEmpty(mimeType))
        {
            if (!_contentTypeProvider.TryGetContentType(fileName, out mimeType))
            {
                mimeType = "application/octet-stream";
            }
        }

        using var stream = fileInfo.CreateReadStream();

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

        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);

        return new ReadResourceResult
        {
            Contents =
            [
                new BlobResourceContents
                {
                    Uri = resource.Resource.Uri,
                    MimeType = mimeType,
                    Blob = memoryStream.ToArray(),
                }
            ]
        };
    }
}
