using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Contact Center Predictive Dialing feature: the Predictive dialing mode, its timings and limits, and the
/// pacing statistics it is sized from.
/// </summary>
/// <remarks>
/// A Predictive profile dials one call per reserved agent, the pacing that cannot abandon. Over-dialing, which places
/// calls before an agent is reserved, builds on the statistics and the calculation registered here and is not
/// available yet.
/// </remarks>
[Feature(ContactCenterConstants.Feature.DialerPredictive)]
public sealed class DialerPredictiveStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerPredictiveStartup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The tenant configuration the options are bound from.</param>
    public DialerPredictiveStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        // Without this feature a Predictive profile resolves to no strategy and the dialer skips it with a warning.
        services.AddScoped<IDialerStrategy, PredictiveDialerStrategy>();

        services
            .AddOptions<ContactCenterPredictiveDialingOptions>()
            .Bind(_shellConfiguration.GetSection(ContactCenterPredictiveDialingOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<ContactCenterPredictiveDialingOptions>, ContactCenterPredictiveDialingOptionsValidator>();

        services
            .AddSingleton<DialerPacingStatisticsCache>()
            .AddScoped<IDialerPacingStatisticsProvider, InteractionEventDialerPacingStatisticsProvider>();
    }
}
