using CrestApps.OrchardCore.OpenAI.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.OpenAI.Core;

internal sealed class DefaultOpenAIOptionsConfiguration : IConfigureOptions<DefaultOpenAIOptions>
{
    private readonly IShellConfiguration _shellConfiguration;

    public DefaultOpenAIOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public void Configure(DefaultOpenAIOptions options)
    {
        _shellConfiguration.GetSection("CrestApps_OpenAI:DefaultParameters").Bind(options);
    }
}
