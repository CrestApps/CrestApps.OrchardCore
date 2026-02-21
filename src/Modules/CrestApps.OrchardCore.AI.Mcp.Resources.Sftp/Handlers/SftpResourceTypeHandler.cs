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
public sealed class SftpResourceTypeHandler : McpResourceTypeHandlerBase
{
    private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    public SftpResourceTypeHandler(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SftpResourceTypeHandler> logger)
        : base(SftpResourceConstants.Type)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    protected override async Task<ReadResourceResult> GetResultAsync(McpResource resource, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        // Get connection details from metadata
        var metadata = resource.As<SftpConnectionMetadata>();

        var host = metadata?.Host;
        if (string.IsNullOrEmpty(host))
        {
            return CreateErrorResult(resource.Resource.Uri, "SFTP host is required in the connection metadata.");
        }

        var port = metadata?.Port ?? 22;
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

        // The path variable is the remote file path.
        variables.TryGetValue("path", out var pathValue);
        var remotePath = "/" + (pathValue ?? string.Empty);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Reading SFTP resource from: {Host}:{Port}{Path}", host, port, remotePath);
        }

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
            return CreateErrorResult(resource.Resource.Uri, "No authentication method provided. Please provide a password or private key.");
        }

        // Build connection info with optional proxy support.
        ConnectionInfo connectionInfo;

        if (Enum.TryParse<ProxyTypes>(metadata?.ProxyType, ignoreCase: true, out var proxyType) &&
            proxyType != ProxyTypes.None &&
            !string.IsNullOrEmpty(metadata?.ProxyHost))
        {
            // Unprotect proxy password if present.
            string proxyPassword = null;
            if (!string.IsNullOrEmpty(metadata?.ProxyPassword))
            {
                try
                {
                    proxyPassword = protector.Unprotect(metadata.ProxyPassword);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to unprotect SFTP proxy password for resource {ResourceId}", resource.ItemId);
                }
            }

            var proxyPort = metadata?.ProxyPort ?? 1080;

            connectionInfo = new ConnectionInfo(
                host, port, username,
                proxyType, metadata.ProxyHost, proxyPort,
                metadata?.ProxyUsername, proxyPassword,
                authMethods.ToArray());
        }
        else
        {
            connectionInfo = new ConnectionInfo(host, port, username, authMethods.ToArray());
        }

        // Apply timeout settings.
        if (metadata?.ConnectionTimeout is > 0)
        {
            connectionInfo.Timeout = TimeSpan.FromSeconds(metadata.ConnectionTimeout.Value);
        }

        // Apply keep-alive interval.
        if (metadata?.KeepAliveInterval is > 0)
        {
            connectionInfo.ChannelCloseTimeout = TimeSpan.FromSeconds(metadata.KeepAliveInterval.Value);
        }

        using var client = new SftpClient(connectionInfo);

        // Apply keep-alive on the client.
        if (metadata?.KeepAliveInterval is > 0)
        {
            client.KeepAliveInterval = TimeSpan.FromSeconds(metadata.KeepAliveInterval.Value);
        }

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
                        Uri = resource.Resource.Uri,
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
