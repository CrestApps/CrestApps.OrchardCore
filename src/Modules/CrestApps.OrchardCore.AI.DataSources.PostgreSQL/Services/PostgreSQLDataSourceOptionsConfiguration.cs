using CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Services;

/// <summary>
/// Configures the <see cref="PostgreSQLDataSourceOptions"/> from the shell configuration.
/// </summary>
public sealed class PostgreSQLDataSourceOptionsConfiguration : IConfigureOptions<PostgreSQLDataSourceOptions>
{
    /// <summary>
    /// The name of the configuration section that holds the PostgreSQL settings shared by every
    /// PostgreSQL feature.
    /// </summary>
    public const string SharedConfigurationSectionName = "CrestApps:PostgreSQL";

    /// <summary>
    /// The name of the configuration section that overrides the shared settings for PostgreSQL
    /// data sources only.
    /// </summary>
    public const string ConfigurationSectionName = "CrestApps:AI:DataSources:PostgreSQL";

    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSQLDataSourceOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration.</param>
    public PostgreSQLDataSourceOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    /// <inheritdoc/>
    public void Configure(PostgreSQLDataSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // The shared section provides the connection used by every PostgreSQL feature, and the
        // data source section overrides it when both are configured.
        _shellConfiguration.GetSection(SharedConfigurationSectionName).Bind(options);
        _shellConfiguration.GetSection(ConfigurationSectionName).Bind(options);

        options.ConnectionString = options.ConnectionString?.Trim();
    }
}
