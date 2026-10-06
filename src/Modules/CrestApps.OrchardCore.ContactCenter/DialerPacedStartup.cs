using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers compliance-gated Power and Progressive paced dialing strategies and scheduled pacing.
/// </summary>
[Feature(ContactCenterConstants.Feature.DialerPaced)]
public sealed class DialerPacedStartup : StartupBase
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerPacedStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialerPacedStartup(IStringLocalizer<DialerPacedStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IDialerStrategy, PowerDialerStrategy>()
            .AddScoped<IDialerStrategy, ProgressiveDialerStrategy>()
            // Predictive is registered by the Predictive Dialing feature, which depends on this one.
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                new ContactCenterFeatureWorkLifecycleParticipant(
                    ContactCenterConstants.Feature.DialerPaced,
                    serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                    serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()));

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
        // source filter matches them too, Predictive included: its profiles need the Predictive Dialing feature, but
        // an activity that carries it is never left out of a Dialer filter.
        services.Configure<ActivitySourceOptions>(options =>
        {
            options.AddSource(ActivitySources.Dialer, entry =>
            {
                entry.Matches(ActivitySources.PowerDial, ActivitySources.ProgressiveDial, ActivitySources.PredictiveDial);
            });
        });
    }
}
