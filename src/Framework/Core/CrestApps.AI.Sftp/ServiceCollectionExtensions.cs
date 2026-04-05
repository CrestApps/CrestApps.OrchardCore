using CrestApps.AI.Mcp;
using CrestApps.AI.Mcp.Models;
using CrestApps.AI.Sftp.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.AI.Sftp;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSftpMcpResourceServices(this IServiceCollection services, Action<McpResourceTypeEntry> configure = null)
        => services.AddMcpResourceType<SftpResourceTypeHandler>(SftpResourceConstants.Type, entry =>
        {
            entry.DisplayName = new LocalizedString("SFTP", "SFTP");
            entry.Description = new LocalizedString("SFTP Description", "Reads content from SFTP servers.");
            entry.SupportedVariables =
            [
                new McpResourceVariable("path") { Description = new LocalizedString("SFTP Path", "The remote file path on the SFTP server.") },
            ];

            configure?.Invoke(entry);
        });
}
