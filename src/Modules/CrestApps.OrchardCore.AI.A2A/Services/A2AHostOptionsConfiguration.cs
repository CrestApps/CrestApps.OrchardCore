using CrestApps.Core.AI.A2A.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.A2A.Services;

/// <summary>
/// Configures <see cref="A2AHostOptions"/> from shell configuration,
/// supporting both the new and deprecated configuration paths.
/// </summary>
internal sealed class A2AHostOptionsConfiguration : IConfigureOptions<A2AHostOptions>
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2AHostOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration.</param>
    public A2AHostOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    /// <summary>
    /// Configures the <see cref="A2AHostOptions"/>.
    /// </summary>
    /// <param name="options">The options instance to configure.</param>
    public void Configure(A2AHostOptions options)
    {
        var deprecatedSection = _shellConfiguration.GetSection("CrestApps:A2AHost");
        var section = _shellConfiguration.GetSection("CrestApps:AI:A2AHost");

        deprecatedSection.Bind(options);
        section.Bind(options);

        if (string.IsNullOrWhiteSpace(section[nameof(A2AHostOptions.AuthenticationType)]) &&
            string.IsNullOrWhiteSpace(deprecatedSection[nameof(A2AHostOptions.AuthenticationType)]))
        {
            options.AuthenticationType = A2AHostAuthenticationType.OpenId;
        }
    }
}
