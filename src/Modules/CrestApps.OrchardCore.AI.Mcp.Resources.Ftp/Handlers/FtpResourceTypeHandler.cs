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
public sealed class FtpResourceTypeHandler : McpResourceTypeHandlerBase
{
    private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    public FtpResourceTypeHandler(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<FtpResourceTypeHandler> logger)
        : base(FtpResourceConstants.Type)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    protected override async Task<ReadResourceResult> GetResultAsync(McpResource resource, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        // Get connection details from metadata
        var metadata = resource.As<FtpConnectionMetadata>();

        var host = metadata?.Host;
        if (string.IsNullOrEmpty(host))
        {
            return CreateErrorResult(resource.Resource.Uri, "FTP host is required in the connection metadata.");
        }

        var port = metadata?.Port ?? 21;

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

        // The path variable is the remote file path.
        variables.TryGetValue("path", out var pathValue);
        var remotePath = "/" + (pathValue ?? string.Empty);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Reading FTP resource from: {Host}:{Port}{Path}", host, port, remotePath);
        }

        using var client = new AsyncFtpClient(host, port);

        if (!string.IsNullOrEmpty(metadata?.Username))
        {
            client.Credentials = new System.Net.NetworkCredential(metadata.Username, password);
        }

        // Apply encryption mode.
        if (Enum.TryParse<FtpEncryptionMode>(metadata?.EncryptionMode, ignoreCase: true, out var encryptionMode))
        {
            client.Config.EncryptionMode = encryptionMode;
        }

        // Apply data connection type.
        if (Enum.TryParse<FtpDataConnectionType>(metadata?.DataConnectionType, ignoreCase: true, out var dataConnectionType))
        {
            client.Config.DataConnectionType = dataConnectionType;
        }

        // Accept any certificate if configured (useful for self-signed certificates).
        if (metadata?.ValidateAnyCertificate == true)
        {
            client.ValidateCertificate += (control, e) => e.Accept = true;
        }

        // Apply timeout settings.
        if (metadata?.ConnectTimeout is > 0)
        {
            client.Config.ConnectTimeout = metadata.ConnectTimeout.Value * 1000;
        }

        if (metadata?.ReadTimeout is > 0)
        {
            client.Config.ReadTimeout = metadata.ReadTimeout.Value * 1000;
        }

        // Apply retry attempts.
        if (metadata?.RetryAttempts is > 0)
        {
            client.Config.RetryAttempts = metadata.RetryAttempts.Value;
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
                        Uri = resource.Resource.Uri,
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
