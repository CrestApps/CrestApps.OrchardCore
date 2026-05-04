using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Represents the default AI options configuration.
/// </summary>
public sealed class DefaultAIOptionsConfiguration : IConfigureOptions<DefaultAIOptions>
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAIOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration.</param>
    public DefaultAIOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    /// <summary>
    /// Configures the .
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(DefaultAIOptions options)
    {
        _shellConfiguration.GetSection("CrestApps_AI:DefaultParameters").Bind(options);
        options.Normalize();
    }
}
