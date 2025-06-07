using CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.OpenAI.Azure;

internal sealed class ElasticsearchServerOptionsConfigurations : IConfigureOptions<ElasticsearchServerOptions>
{
    public const string ConfigSectionName = "OrchardCore_Elasticsearch";

    private readonly IShellConfiguration _shellConfiguration;

    public ElasticsearchServerOptionsConfigurations(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public void Configure(ElasticsearchServerOptions options)
    {
        _shellConfiguration.GetSection(ConfigSectionName).Bind(options);

        if (options.Ports == null || options.Ports.Length == 0)
        {
            options.Ports = [9200];
        }

        if (!string.IsNullOrWhiteSpace(options.Url))
        {
            options.SetFileConfigurationExists(true);
        }
    }
}
