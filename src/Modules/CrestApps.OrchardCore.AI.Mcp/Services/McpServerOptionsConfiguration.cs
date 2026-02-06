using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

internal sealed class McpServerOptionsConfiguration : IConfigureOptions<McpServerOptions>
{
    private readonly IShellConfiguration _shellConfiguration;

    public McpServerOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public void Configure(McpServerOptions options)
    {
        _shellConfiguration.GetSection("CrestApps_AI:McpServer").Bind(options);
    }
}
