using CrestApps.Core.AI.Mcp.Models;
using CrestApps.OrchardCore.AI.Mcp.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

internal sealed class McpServerOptionsConfiguration : IConfigureOptions<McpServerOptions>
{
    private readonly ISiteService _siteService;
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpServerOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="shellConfiguration">The shell configuration.</param>
    public McpServerOptionsConfiguration(
        ISiteService siteService,
        IShellConfiguration shellConfiguration)
    {
        _siteService = siteService;
        _shellConfiguration = shellConfiguration;
    }

    /// <summary>
    /// Configures the <see cref="McpServerOptions"/> from the stored site settings, then lets the shell
    /// configuration (for example <c>appsettings.json</c>) override the values for deployment scenarios.
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(McpServerOptions options)
    {
        var settings = _siteService.GetSettings<McpServerSettings>();

        options.AuthenticationType = settings.AuthenticationType;
        options.ApiKey = settings.ApiKey;
        options.RequireAccessPermission = settings.RequireAccessPermission;
        options.ExposeAllTools = settings.ExposeAllTools;
        options.Tools = settings.Tools is null
            ? []
            : [.. settings.Tools];

        // Preserve backward compatibility by allowing the shell configuration to override the settings.
        var deprecatedSection = _shellConfiguration.GetSection("CrestApps:McpServer");
        var section = _shellConfiguration.GetSection("CrestApps:AI:McpServer");

        deprecatedSection.Bind(options);
        section.Bind(options);
    }
}
