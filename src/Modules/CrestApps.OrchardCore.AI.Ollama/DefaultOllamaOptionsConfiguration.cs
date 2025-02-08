using CrestApps.OrchardCore.AI.Ollama;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.DeepSeek.Core;

internal sealed class DefaultOllamaOptionsConfiguration : IConfigureOptions<DefaultOllamaOptions>
{
    private readonly IShellConfiguration _shellConfiguration;

    public DefaultOllamaOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public void Configure(DefaultOllamaOptions options)
    {
        _shellConfiguration.GetSection("CrestApps_AI:Ollama:DefaultParameters").Bind(options);
    }
}
