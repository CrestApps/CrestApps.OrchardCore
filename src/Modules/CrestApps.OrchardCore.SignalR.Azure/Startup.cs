using CrestApps.OrchardCore.SignalR.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.SignalR.Azure;

/// <summary>
/// Registers the Azure SignalR Service backplane, using the connection string configured under the
/// <c>CrestApps:SignalR:Azure:ConnectionString</c> key.
/// </summary>
[Feature(SignalRConstants.Feature.AzureBackplane)]
public sealed class Startup : StartupBase
{
    private const string ConfigurationSection = "CrestApps:SignalR:Azure";

    private readonly IShellConfiguration _shellConfiguration;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration.</param>
    /// <param name="logger">The logger.</param>
    public Startup(
        IShellConfiguration shellConfiguration,
        ILogger<Startup> logger)
    {
        _shellConfiguration = shellConfiguration;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        var connectionString = _shellConfiguration
            .GetSection(ConfigurationSection)
            .GetValue<string>("ConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogWarning(
                "The '{Feature}' feature is enabled but '{Section}:ConnectionString' is not configured.",
                SignalRConstants.Feature.AzureBackplane,
                ConfigurationSection);

            return;
        }

        _logger.LogInformation("The Azure SignalR Service backplane is enabled.");

        services
            .AddSignalR()
            .AddAzureSignalR(connectionString);
    }
}
