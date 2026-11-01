using CrestApps.OrchardCore.AI.WebMcp.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Loads the stored <see cref="WebMcpSettings"/> into <see cref="WebMcpOptions"/>, then lets the
/// <c>CrestApps:AI:WebMcp</c> shell configuration section override them.
/// </summary>
internal sealed class WebMcpOptionsConfiguration : IConfigureOptions<WebMcpOptions>
{
    private readonly ISiteService _siteService;
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebMcpOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="shellConfiguration">The shell configuration.</param>
    public WebMcpOptionsConfiguration(
        ISiteService siteService,
        IShellConfiguration shellConfiguration)
    {
        _siteService = siteService;
        _shellConfiguration = shellConfiguration;
    }

    /// <summary>
    /// Configures the options from the stored settings and the shell configuration.
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(WebMcpOptions options)
    {
        var settings = _siteService.GetSettings<WebMcpSettings>();

        options.ExposeAllTools = settings.ExposeAllTools;
        options.Tools = settings.Tools is null
            ? []
            : [.. settings.Tools];
        options.EnableOnAdminPages = settings.EnableOnAdminPages;
        options.EnableOnFrontEndPages = settings.EnableOnFrontEndPages;

        _shellConfiguration.GetSection(WebMcpConstants.ConfigurationSection).Bind(options);
    }
}
