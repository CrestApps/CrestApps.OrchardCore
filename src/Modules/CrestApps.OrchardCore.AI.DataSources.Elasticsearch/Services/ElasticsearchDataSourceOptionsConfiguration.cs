using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;

/// <summary>
/// Configures the <see cref="ElasticsearchDataSourceOptions"/> from the shell configuration.
/// </summary>
public sealed class ElasticsearchDataSourceOptionsConfiguration : IConfigureOptions<ElasticsearchDataSourceOptions>
{
    /// <summary>
    /// The name of the configuration section that holds the Elasticsearch settings shared by every
    /// Elasticsearch feature.
    /// </summary>
    public const string SharedConfigurationSectionName = "CrestApps:Elasticsearch";

    /// <summary>
    /// The name of the configuration section that overrides the shared settings for Elasticsearch
    /// data sources only.
    /// </summary>
    public const string ConfigurationSectionName = "CrestApps:AI:DataSources:Elasticsearch";

    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElasticsearchDataSourceOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration.</param>
    public ElasticsearchDataSourceOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    /// <inheritdoc/>
    public void Configure(ElasticsearchDataSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // The shared section provides the connection used by every Elasticsearch feature, and the
        // data source section overrides it when both are configured.
        _shellConfiguration.GetSection(SharedConfigurationSectionName).Bind(options);
        _shellConfiguration.GetSection(ConfigurationSectionName).Bind(options);

        options.Url = options.Url?.Trim();
        options.CloudId = options.CloudId?.Trim();
        options.AuthenticationType = options.AuthenticationType?.Trim();
        options.Username = options.Username?.Trim();
        options.ApiKeyId = options.ApiKeyId?.Trim();
        options.CertificateFingerprint = options.CertificateFingerprint?.Trim();
    }
}
