using System.ComponentModel.DataAnnotations;
using CrestApps.Core;
using CrestApps.Core.AI.Ftp;
using CrestApps.Core.AI.Ftp.Models;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Sftp;
using CrestApps.Core.AI.Sftp.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.AI.FileTransfer.Core.Services;

/// <summary>
/// Refuses an FTP or SFTP MCP resource whose server or proxy is on the site's own network when it is saved through the
/// catalog: the editor, the API and the recipe step.
/// </summary>
internal sealed class FileTransferResourceEgressHandler : CatalogEntryHandlerBase<McpResource>
{
    private readonly FileTransferEgressPolicy _policy;

    public FileTransferResourceEgressHandler(FileTransferEgressPolicy policy)
    {
        _policy = policy;
    }

    public override Task ValidatingAsync(ValidatingContext<McpResource> context, CancellationToken cancellationToken = default)
    {
        var resource = context.Model;

        var ftp = string.Equals(resource?.Source, FtpResourceConstants.Type, StringComparison.OrdinalIgnoreCase) && resource.TryGet<FtpConnectionMetadata>(out var metadata)
            ? metadata
            : null;

        var hosts = string.Equals(resource?.Source, FtpResourceConstants.Type, StringComparison.OrdinalIgnoreCase)
            ? FileTransferEgress.FtpHosts(ftp)
            : string.Equals(resource?.Source, SftpResourceConstants.Type, StringComparison.OrdinalIgnoreCase)
                ? FileTransferEgress.SftpHosts(resource.TryGet<SftpConnectionMetadata>(out var sftp) ? sftp : null)
                : [];

        foreach (var (field, host) in hosts)
        {
            if (!_policy.IsAllowed(host))
            {
                context.Result.Fail(new ValidationResult(FileTransferEgress.RefusedMessage, [field]));
            }
        }

        if (ftp is not null && FileTransferEgress.FtpDataConnectionRefusal(ftp, _policy) is { } dataConnectionField)
        {
            context.Result.Fail(new ValidationResult(FileTransferEgress.DataConnectionRefusedMessage, [dataConnectionField]));
        }

        return Task.CompletedTask;
    }
}
