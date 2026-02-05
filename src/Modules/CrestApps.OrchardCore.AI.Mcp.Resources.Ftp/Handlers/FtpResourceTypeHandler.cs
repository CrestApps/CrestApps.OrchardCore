using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using FluentFTP;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.Handlers;

/// <summary>
/// Handles ftp:// and ftps:// URI resources by reading content from FTP servers.
/// Connection details are stored in the resource's Properties.
/// </summary>
public sealed class FtpResourceTypeHandler : IMcpResourceTypeHandler
{
    private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();
    private readonly ILogger _logger;

    public FtpResourceTypeHandler(ILogger<FtpResourceTypeHandler> logger)
    {
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

        // Parse the ftp:// or ftps:// URI
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var ftpUri) ||
            (ftpUri.Scheme != "ftp" && ftpUri.Scheme != "ftps"))
        {
            throw new InvalidOperationException($"Invalid FTP URI: {uri}. Expected format: ftp://host/path or ftps://host/path");
        }

        // Get connection details from resource properties
        var host = GetPropertyValue(resource, FtpResourceConstants.Settings.Host) ?? ftpUri.Host;
        var portString = GetPropertyValue(resource, FtpResourceConstants.Settings.Port);
        var port = int.TryParse(portString, out var p) ? p : (ftpUri.IsDefaultPort ? 21 : ftpUri.Port);
        var username = GetPropertyValue(resource, FtpResourceConstants.Settings.Username);
        var password = GetPropertyValue(resource, FtpResourceConstants.Settings.Password);
        var useSslString = GetPropertyValue(resource, FtpResourceConstants.Settings.UseSsl);
        var useSsl = bool.TryParse(useSslString, out var ssl) ? ssl : ftpUri.Scheme == "ftps";

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

    private static string GetPropertyValue(McpResource resource, string key)
    {
        if (resource.Properties is not null && resource.Properties.ContainsKey(key))
        {
            return resource.Properties[key]?.ToString();
        }

        return null;
    }
}
