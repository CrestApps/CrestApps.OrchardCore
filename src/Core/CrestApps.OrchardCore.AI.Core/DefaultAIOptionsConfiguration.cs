using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Represents the default AI options configuration.
/// </summary>
public sealed class DefaultAIOptionsConfiguration : IConfigureOptions<DefaultAIOptions>
{
    private readonly IShellConfiguration _shellConfiguration;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAIOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration.</param>
    /// <param name="logger">The logger.</param>
    public DefaultAIOptionsConfiguration(
        IShellConfiguration shellConfiguration,
        ILogger<DefaultAIOptionsConfiguration> logger)
    {
        _shellConfiguration = shellConfiguration;
        _logger = logger;
    }

    /// <summary>
    /// Configures the <see cref="DefaultAIOptions"/>.
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(DefaultAIOptions options)
    {
        var deprecatedPath = "CrestApps_AI:DefaultParameters";
        var newPath = "CrestApps:AI:DefaultParameters";
        var deprecatedSection = _shellConfiguration.GetSection(deprecatedPath);

        if (deprecatedSection.Exists())
        {
            _logger.LogWarning(
                """
                The configuration section '{DeprecatedPath}' is deprecated and will be removed in a future version.
                Please migrate your settings to '{NewPath}'.
                In appsettings.json, use the OrchardCore:CrestApps:AI:DefaultParameters path instead of OrchardCore:{DeprecatedKey}.
                """,
                deprecatedPath,
                newPath,
                "CrestApps_AI");

            deprecatedSection.Bind(options);
        }

        _shellConfiguration.GetSection(newPath).Bind(options);
        options.Normalize();
    }
}
