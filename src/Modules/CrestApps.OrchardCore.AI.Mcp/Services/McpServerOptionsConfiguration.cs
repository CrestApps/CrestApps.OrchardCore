using CrestApps.Core.AI.Mcp.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

internal sealed class McpServerOptionsConfiguration : IConfigureOptions<McpServerOptions>
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpServerOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration.</param>
    public McpServerOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    /// <summary>
    /// Configures the .
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(McpServerOptions options)
    {
        var section = _shellConfiguration.GetSection("CrestApps:McpServer");

        section.Bind(options);

        if (string.IsNullOrWhiteSpace(section[nameof(McpServerOptions.AuthenticationType)]))
        {
            options.AuthenticationType = McpServerAuthenticationType.OpenId;
        }
    }
}
