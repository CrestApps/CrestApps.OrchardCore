using CrestApps.Core.AI.Ftp.Models;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using FluentFTP;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace CrestApps.Core.AI.Ftp.Handlers;

public sealed class FtpResourceTypeHandler : McpResourceTypeHandlerBase
{
    private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<FtpResourceTypeHandler> _logger;

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
        var metadata = resource.As<FtpConnectionMetadata>();
        var host = metadata?.Host;

        if (string.IsNullOrEmpty(host))
        {
            return CreateErrorResult(resource.Resource.Uri, "FTP host is required in the connection metadata.");
        }

        var port = metadata.Port ?? 21;
        var remotePath = "/" + (variables.TryGetValue("path", out var pathValue) ? pathValue : string.Empty);
        string password = null;

        if (!string.IsNullOrEmpty(metadata.Password))
        {
            try
            {
                password = _dataProtectionProvider.CreateProtector(FtpResourceConstants.DataProtectionPurpose).Unprotect(metadata.Password);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to unprotect FTP password for resource {ResourceId}", resource.ItemId);
            }
        }

        using var client = new AsyncFtpClient(host, port);

        if (!string.IsNullOrEmpty(metadata.Username))
        {
            client.Credentials = new System.Net.NetworkCredential
            {
                UserName = metadata.Username,
                Password = password,
            };
        }

        if (Enum.TryParse<FtpEncryptionMode>(metadata.EncryptionMode, true, out var encryptionMode))
        {
            client.Config.EncryptionMode = encryptionMode;
        }

        if (Enum.TryParse<FtpDataConnectionType>(metadata.DataConnectionType, true, out var dataConnectionType))
        {
            client.Config.DataConnectionType = dataConnectionType;
        }

        if (metadata.ValidateAnyCertificate)
        {
            client.ValidateCertificate += (_, args) => args.Accept = true;
        }

        if (metadata.ConnectTimeout is > 0)
        {
            client.Config.ConnectTimeout = metadata.ConnectTimeout.Value * 1000;
        }

        if (metadata.ReadTimeout is > 0)
        {
            client.Config.ReadTimeout = metadata.ReadTimeout.Value * 1000;
        }

        if (metadata.RetryAttempts is > 0)
        {
            client.Config.RetryAttempts = metadata.RetryAttempts.Value;
        }

        await client.Connect(cancellationToken);

        try
        {
            using var stream = new MemoryStream();
            await client.DownloadStream(stream, remotePath, token: cancellationToken);
            stream.Position = 0;

            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync(cancellationToken);

            var mimeType = resource.Resource?.MimeType;

            if (string.IsNullOrEmpty(mimeType) && !_contentTypeProvider.TryGetContentType(remotePath, out mimeType))
            {
                mimeType = "application/octet-stream";
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
