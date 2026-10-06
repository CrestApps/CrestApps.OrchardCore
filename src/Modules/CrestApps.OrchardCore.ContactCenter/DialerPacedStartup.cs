using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the compliance-gated Power, Progressive and Predictive paced dialing strategies, scheduled pacing, and the
/// timings, limits and pacing statistics Predictive dialing is sized from.
/// </summary>
/// <remarks>
/// Predictive dialing is part of this feature rather than a feature of its own: what a Predictive profile may do beyond
/// one call per reserved agent is decided by the profile's pacing model, whose safeguards (an enforced abandonment cap,
/// the abandoned-call message and a target below the cap) are validated on every save, import and deployment.
/// </remarks>
[Feature(ContactCenterConstants.Feature.DialerPaced)]
public sealed class DialerPacedStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerPacedStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="shellConfiguration">The tenant configuration the predictive dialing options are bound from.</param>
    public DialerPacedStartup(
        IStringLocalizer<DialerPacedStartup> stringLocalizer,
        IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IDialerStrategy, PowerDialerStrategy>()
            .AddScoped<IDialerStrategy, ProgressiveDialerStrategy>()
            .AddScoped<IDialerStrategy, PredictiveDialerStrategy>()
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                new ContactCenterFeatureWorkLifecycleParticipant(
                    ContactCenterConstants.Feature.DialerPaced,
                    serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                    serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()));

        // Predictive dialing's timings and limits, and the measurements its pacing is sized from.
        services
            .AddOptions<ContactCenterPredictiveDialingOptions>()
            .Bind(_shellConfiguration.GetSection(ContactCenterPredictiveDialingOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<ContactCenterPredictiveDialingOptions>, ContactCenterPredictiveDialingOptionsValidator>();

        services
            .AddSingleton<DialerPacingStatisticsCache>()
            .AddScoped<IDialerPacingStatisticsProvider, InteractionEventDialerPacingStatisticsProvider>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, DialerPacingBackgroundTask>());

        services.Configure<ActivityBatchSourceOptions>(options =>
        {
            options.AddSource(ActivitySources.PowerDial, entry =>
            {
                entry.DisplayName = S["Power dial batch"];
                entry.Description = S["Loads unassigned activities the dialer dials automatically for available agents."];
                entry.RequiresUserAssignment = false;
                entry.ShowInCreationPicker = false;
            });
        });

        // The paced modes are stored on the activities of a dialer load whose profile uses them, so the Dialer
        // source filter matches them too.
        services.Configure<ActivitySourceOptions>(options =>
        {
            options.AddSource(ActivitySources.Dialer, entry =>
            {
                entry.Matches(ActivitySources.PowerDial, ActivitySources.ProgressiveDial, ActivitySources.PredictiveDial);
            });
        });
    }
}
