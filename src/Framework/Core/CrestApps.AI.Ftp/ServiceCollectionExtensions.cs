using CrestApps.AI.Ftp.Handlers;
using CrestApps.AI.Mcp;
using CrestApps.AI.Mcp.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.AI.Ftp;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFtpMcpResourceServices(this IServiceCollection services, Action<McpResourceTypeEntry> configure = null)
        => services.AddMcpResourceType<FtpResourceTypeHandler>(FtpResourceConstants.Type, entry =>
        {
            entry.DisplayName = new LocalizedString("FTP", "FTP/FTPS");
            entry.Description = new LocalizedString("FTP Description", "Reads content from FTP/FTPS servers.");
            entry.SupportedVariables =
            [
                new McpResourceVariable("path") { Description = new LocalizedString("FTP Path", "The remote file path on the FTP server.") },
            ];

            configure?.Invoke(entry);
        });
}
