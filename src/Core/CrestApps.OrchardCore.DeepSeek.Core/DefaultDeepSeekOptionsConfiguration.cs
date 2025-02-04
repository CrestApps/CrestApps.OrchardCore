using CrestApps.OrchardCore.DeepSeek.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.DeepSeek.Core;

internal sealed class DefaultDeepSeekOptionsConfiguration : IConfigureOptions<DefaultDeepSeekOptions>
{
    private readonly IShellConfiguration _shellConfiguration;

    public DefaultDeepSeekOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public void Configure(DefaultDeepSeekOptions options)
    {
        _shellConfiguration.GetSection("CrestApps_AI:DeepSeek:DefaultParameters").Bind(options);
    }
}
