using CrestApps.OrchardCore.Asterisk.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Configures the configuration-backed default Asterisk provider from shell configuration.
/// </summary>
public sealed class DefaultAsteriskOptionsConfiguration : IConfigureOptions<DefaultAsteriskOptions>
{
    private readonly IShellConfiguration _shellConfiguration;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAsteriskOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration.</param>
    /// <param name="logger">The logger.</param>
    public DefaultAsteriskOptionsConfiguration(
        IShellConfiguration shellConfiguration,
        ILogger<DefaultAsteriskOptionsConfiguration> logger)
    {
        _shellConfiguration = shellConfiguration;
        _logger = logger;
    }

    /// <inheritdoc/>
    public void Configure(DefaultAsteriskOptions options)
    {
        var section = _shellConfiguration.GetSection(AsteriskConstants.DefaultConfigurationSectionPath);

        section.Bind(options);
        AsteriskSettingsUtilities.ApplyDefaults(options);
        options.IsEnabled = AsteriskSettingsUtilities.HasRequiredConfiguration(options, options.Password);

        if (section.Exists() && !options.IsEnabled)
        {
            _logger.LogWarning(
                "The default Asterisk provider configuration section '{SectionPath}' is present but incomplete. BaseUrl, UserName, Password, and ApplicationName are required for the provider to be available.",
                AsteriskConstants.DefaultConfigurationSectionPath);
        }
    }
}
