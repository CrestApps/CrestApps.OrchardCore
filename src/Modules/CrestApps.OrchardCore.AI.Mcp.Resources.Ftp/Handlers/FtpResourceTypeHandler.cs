using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.Models;
using FluentFTP;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.Handlers;

/// <summary>
/// Handles ftp:// URI resources by reading content from FTP/FTPS servers.
/// For FTPS (secure FTP), enable UseSsl in the connection metadata.
/// Connection details are stored in the resource's FtpConnectionMetadata.
/// </summary>
public sealed class FtpResourceTypeHandler : IMcpResourceTypeHandler
{
    private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    public FtpResourceTypeHandler(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<FtpResourceTypeHandler> logger)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    public string Type => FtpResourceConstants.Type;

    public async Task<ReadResourceResult> ReadAsync(McpResource resource, CancellationToken cancellationToken = default)
    {
        var uri = resource.Resource?.Uri;

        if (string.IsNullOrEmpty(uri))
        {
            throw new InvalidOperationException("Resource URI is required.");
        }

        // Parse the ftp:// URI - ftps is handled via UseSsl metadata setting
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var ftpUri) || ftpUri.Scheme != "ftp")
        {
            throw new InvalidOperationException($"Invalid FTP URI: {uri}. Expected format: ftp://host/path");
        }

        // Get connection details from metadata
        var metadata = resource.As<FtpConnectionMetadata>();

        var host = metadata?.Host ?? ftpUri.Host;
        var port = metadata?.Port ?? (ftpUri.IsDefaultPort ? 21 : ftpUri.Port);
        var username = metadata?.Username;
        var useSsl = metadata?.UseSsl ?? false;

        // Unprotect password if present
        string password = null;
        if (!string.IsNullOrEmpty(metadata?.Password))
        {
            try
            {
                var protector = _dataProtectionProvider.CreateProtector(FtpResourceConstants.DataProtectionPurpose);
                password = protector.Unprotect(metadata.Password);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to unprotect FTP password for resource {ResourceId}", resource.ItemId);
            }
        }

        var remotePath = ftpUri.AbsolutePath;

        _logger.LogDebug("Reading FTP resource from: {Host}:{Port}{Path}", host, port, remotePath);

        using var client = new AsyncFtpClient(host, port);

        if (!string.IsNullOrEmpty(username))
        {
            client.Credentials = new System.Net.NetworkCredential(username, password);
        }

        if (useSsl)
        {
            client.Config.EncryptionMode = FtpEncryptionMode.Explicit;
        }

        await client.Connect(cancellationToken);

        try
        {
            // Download file content
            using var stream = new MemoryStream();
            await client.DownloadStream(stream, remotePath, token: cancellationToken);
            stream.Position = 0;

            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync(cancellationToken);

            // Determine MIME type
            var mimeType = resource.Resource?.MimeType;
            if (string.IsNullOrEmpty(mimeType))
            {
                if (!_contentTypeProvider.TryGetContentType(remotePath, out mimeType))
                {
                    mimeType = "application/octet-stream";
                }
            }

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
        finally
        {
            await client.Disconnect(cancellationToken);
        }
    }
}
