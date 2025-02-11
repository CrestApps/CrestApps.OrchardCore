using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.Core;

public sealed class DefaultAIOptionsConfiguration : IConfigureOptions<DefaultAIOptions>
{
    private readonly IShellConfiguration _shellConfiguration;

    public DefaultAIOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public void Configure(DefaultAIOptions options)
    {
        _shellConfiguration.GetSection("CrestApps_AI:DefaultParameters").Bind(options);
    }
}
