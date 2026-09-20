using CrestApps.Core.ContactCenter;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using CrestApps.OrchardCore.ContactCenter.Core;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers compliance-gated Power and Progressive paced dialing strategies and scheduled pacing.
/// </summary>
[Feature(ContactCenterFeatures.DialerPaced)]
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
        services.AddContactCenterCapability(ContactCenterFeatures.DialerPaced, ContactCenterCapabilities.DialerPaced);

        services.AddCoreContactCenterPacedDialing();

        services
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                new ContactCenterFeatureWorkLifecycleParticipant(
                    ContactCenterCapabilities.DialerPaced,
                    serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                    serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()));

        // The host's scheduler for the cycle AddCoreContactCenterPacedDialing registered.
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
    }
}
