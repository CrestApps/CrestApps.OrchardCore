using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using OrchardCore.Entities;
using Renci.SshNet;

namespace CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.Handlers;

/// <summary>
/// Handles sftp:// URI resources by reading content from SFTP servers.
/// Connection details are stored in the resource's SftpConnectionMetadata.
/// </summary>
public sealed class SftpResourceTypeHandler : IMcpResourceTypeHandler
{
    private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    public SftpResourceTypeHandler(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SftpResourceTypeHandler> logger)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    public string Type => SftpResourceConstants.Type;

    public async Task<ReadResourceResult> ReadAsync(McpResource resource, CancellationToken cancellationToken = default)
    {
        var uri = resource.Resource?.Uri;

        if (string.IsNullOrEmpty(uri))
        {
            throw new InvalidOperationException("Resource URI is required.");
        }

        // Parse the sftp:// URI
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var sftpUri) || sftpUri.Scheme != "sftp")
        {
            throw new InvalidOperationException($"Invalid SFTP URI: {uri}. Expected format: sftp://host/path");
        }

        // Get connection details from metadata
        var metadata = resource.As<SftpConnectionMetadata>();

        var host = metadata?.Host ?? sftpUri.Host;
        var port = metadata?.Port ?? (sftpUri.IsDefaultPort ? 22 : sftpUri.Port);
        var username = metadata?.Username;

        // Unprotect credentials if present
        var protector = _dataProtectionProvider.CreateProtector(SftpResourceConstants.DataProtectionPurpose);

        string password = null;
        if (!string.IsNullOrEmpty(metadata?.Password))
        {
            try
            {
                password = protector.Unprotect(metadata.Password);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to unprotect SFTP password for resource {ResourceId}", resource.ItemId);
            }
        }

        string privateKey = null;
        if (!string.IsNullOrEmpty(metadata?.PrivateKey))
        {
            try
            {
                privateKey = protector.Unprotect(metadata.PrivateKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to unprotect SFTP private key for resource {ResourceId}", resource.ItemId);
            }
        }

        string passphrase = null;
        if (!string.IsNullOrEmpty(metadata?.Passphrase))
        {
            try
            {
                passphrase = protector.Unprotect(metadata.Passphrase);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to unprotect SFTP passphrase for resource {ResourceId}", resource.ItemId);
            }
        }

        var remotePath = sftpUri.AbsolutePath;

        _logger.LogDebug("Reading SFTP resource from: {Host}:{Port}{Path}", host, port, remotePath);

        // Build authentication methods
        var authMethods = new List<AuthenticationMethod>();

        if (!string.IsNullOrEmpty(privateKey))
        {
            using var keyStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(privateKey));
            var privateKeyFile = string.IsNullOrEmpty(passphrase)
                ? new PrivateKeyFile(keyStream)
                : new PrivateKeyFile(keyStream, passphrase);
            authMethods.Add(new PrivateKeyAuthenticationMethod(username, privateKeyFile));
        }

        if (!string.IsNullOrEmpty(password))
        {
            authMethods.Add(new PasswordAuthenticationMethod(username, password));
        }

        if (authMethods.Count == 0)
        {
            throw new InvalidOperationException("No authentication method provided. Please provide a password or private key.");
        }

        var connectionInfo = new ConnectionInfo(host, port, username, authMethods.ToArray());

        using var client = new SftpClient(connectionInfo);

        await Task.Run(() => client.Connect(), cancellationToken);

        try
        {
            // Download file content
            using var stream = new MemoryStream();
            client.DownloadFile(remotePath, stream);
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
            client.Disconnect();
        }
    }
}
