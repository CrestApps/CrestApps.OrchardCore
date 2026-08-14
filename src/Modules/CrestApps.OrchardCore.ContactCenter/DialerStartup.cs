using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers outbound dialing profiles, callbacks, and agent-driven activity batch sources.
/// </summary>
[Feature(ContactCenterConstants.Feature.Dialer)]
public sealed class DialerStartup : StartupBase
{
    private readonly IStringLocalizer S;

    public DialerStartup(IStringLocalizer<DialerStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IDialerProfileStore, DialerProfileStore>()
            .AddScoped<IDialerProfileManager, DialerProfileManager>()
            .AddScoped<ICallbackRequestStore, CallbackRequestStore>()
            .AddScoped<ICallbackRequestManager, CallbackRequestManager>()
            .AddScoped<IContactCenterRetentionPolicy, CallbackRequestRetentionPolicy>()
            .AddScoped<ICallbackService, CallbackService>()
            .AddScoped<IDialerService, DialerService>()
            .AddScoped<IActivityDialerContributor, ContactCenterActivityDialerContributor>()
            .AddScoped<IDialerStrategyResolver, DialerStrategyResolver>()
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                new ContactCenterFeatureWorkLifecycleParticipant(
                    ContactCenterConstants.Feature.Dialer,
                    serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                    serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()));

        services
            .AddScoped<ICatalogEntryHandler<DialerProfile>, DialerProfileHandler>()
            .AddIndexProvider<DialerProfileIndexProvider>()
            .AddDataMigration<DialerProfileIndexMigrations>()
            .AddIndexProvider<CallbackRequestIndexProvider>()
            .AddDataMigration<CallbackRequestIndexMigrations>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, CallbackDispatchBackgroundTask>());

        services.Configure<ActivityBatchSourceOptions>(options =>
        {
            options.AddSource(ActivitySources.Dialer, entry =>
            {
                entry.DisplayName = S["Dialer"];
                entry.Description = S["Loads unassigned activities and applies the selected dialer profile when the batch is loaded."];
                entry.RequiresUserAssignment = false;
            });

            options.AddSource(ActivitySources.PreviewDial, entry =>
            {
                entry.DisplayName = S["Preview dial batch"];
                entry.Description = S["Loads unassigned activities the dialer offers to agents one at a time for review before dialing."];
                entry.RequiresUserAssignment = false;
                entry.ShowInCreationPicker = false;
            });

        });
    }
}
