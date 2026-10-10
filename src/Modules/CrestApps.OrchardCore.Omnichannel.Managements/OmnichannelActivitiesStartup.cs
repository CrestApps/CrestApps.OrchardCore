using CrestApps.Core;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Managements.Endpoints;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Migrations;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Managements;

/// <summary>
/// Registers the omnichannel activity, campaign, disposition, channel-endpoint, and subject-flow services that
/// carry no administration user interface.
/// </summary>
/// <remarks>
/// These services used to be registered by the administration feature, which meant that anything needing to read
/// or write a CRM activity - the whole Contact Center queue, routing and voice stack among them - transitively
/// enabled the administration screens, their content-type editors, their client resources and their admin menu.
/// A deployment that exposes only an API had no way to decline them. They are separated here so the data and
/// behaviour can be enabled without the screens, and the administration feature depends on this one so an
/// existing tenant that has the screens keeps everything it had.
/// </remarks>
[Feature(OmnichannelConstants.Features.Activities)]
public sealed class OmnichannelActivitiesStartup : StartupBase
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivitiesStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer used for the option display names contributed here.</param>
    public OmnichannelActivitiesStartup(IStringLocalizer<OmnichannelActivitiesStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCatalogs()
            .AddCatalogManagers();

        services.AddScoped<IActivityBatchLoadCoordinator, DefaultActivityBatchLoadCoordinator>();
        services.AddScoped<DefaultContactActivityBatchLoader>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, AutomatedActivitiesProcessorBackgroundTask>());

        // Shared with the Contact Center's outbound screening, so the loader and the dialler answer "may we reach
        // them?" the same way. Whichever feature registers it first provides it.
        services.TryAddScoped<IContactOptOutResolver, ContactOptOutResolver>();

        // The load editor and the automated sends take the business-hours gate as a plain dependency, and the
        // contract is declared here, in Omnichannel, so its always-open default is registered here too. The business
        // hours feature replaces it with the calendar-backed gate. It used to be registered by the Contact Center
        // feature only, so a tenant without Contact Center could not open Load Activities.
        services.TryAddScoped<IBusinessHoursGate, AlwaysOpenBusinessHoursGate>();

        // Which content types are contacts and which are leads, for channels that match callers and search
        // contacts without the administration screens. Whichever feature registers it first provides it.
        services.TryAddScoped<IOmnichannelContactTypeProvider, ContentDefinitionOmnichannelContactTypeProvider>();

        services
            .AddYesSqlDocumentCatalog<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>(collection: OmnichannelConstants.CollectionName)
            .AddScoped<IOmnichannelActivityStore, OmnichannelActivityStore>()
            .AddScoped<IOmnichannelActivityManager, OmnichannelActivityManager>()
            .AddScoped<IOmnichannelChannelEndpointStore, OmnichannelChannelEndpointStore>()
            .AddScoped<IOmnichannelChannelEndpointManager, OmnichannelChannelEndpointManager>()
            .AddDataMigration<OmnichannelAddressMigrations>()
            .AddScoped<ICatalogEntryHandler<OmnichannelActivityBatch>, OmnichannelActivityBatchHandler>()
            .AddIndexProvider<OmnichannelActivityBatchIndexProvider>()
            .AddDataMigration<OmnichannelActivityBatchIndexMigrations>();

        // The kinds of address the business can own. A type is offered in the address list once a feature registers a
        // capability for it, so email addresses stay hidden until an email channel exists.
        services.AddOmnichannelAddressType(OmnichannelAddressTypes.PhoneNumber, type =>
        {
            type.DisplayName = S["Phone number"];
            type.Description = S["A phone number the business owns, for calls, texts or both."];
        });

        services.AddOmnichannelAddressType(OmnichannelAddressTypes.EmailAddress, type =>
        {
            type.DisplayName = S["Email address"];
            type.Description = S["An email address the business owns."];
        });

        // Reusable re-engagement cadences selected on automated loading campaigns.
        services
            .AddYesSqlDocumentCatalog<Cadence, CadenceIndex>(collection: OmnichannelConstants.CollectionName)
            .AddScoped<ICatalogEntryHandler<Cadence>, CadenceHandler>()
            .AddIndexProvider<CadenceIndexProvider>()
            .AddDataMigration<CadenceIndexMigrations>()
            .AddDataMigration<DocumentTypeColumnMigrations>();

        // Numbers known not to be in service, read by the loader, the dialer and the automated caller alike.
        services
            .AddScoped<INotInServiceNumberService, NotInServiceNumberService>()
            .AddScoped<INotInServiceActivityCompleter, NotInServiceActivityCompleter>()
            .AddScoped<IActivityOutcomeCompleter, ActivityOutcomeCompleter>()
            .AddIndexProvider<NotInServiceNumberIndexProvider>()
            .AddDataMigration<NotInServiceNumberIndexMigrations>();

        services.AddContentPart<OmnichannelContactPart>();
        services.AddContentPart<OmnichannelSubjectPart>();
        services.AddScoped<OmnichannelContactDefinitionService>();
        services.AddScoped<IContentDefinitionHandler, OmnichannelContactDefinitionHandler>();

        services.AddScoped<ICatalogEntryHandler<OmnichannelDisposition>, OmnichannelDispositionHandler>();
        services.AddScoped<ICatalogEntryHandler<OmnichannelCampaign>, OmnichannelCampaignHandler>();
        services.AddScoped<ICatalogEntryHandler<OmnichannelCampaignGroup>, OmnichannelCampaignGroupHandler>();
        services.AddScoped<ICatalogEntryHandler<OmnichannelChannelEndpoint>, OmnichannelChannelEndpointHandler>();
        services.AddScoped<ICatalogEntryHandler<SubjectAction>, SubjectActionHandler>();

        services
            .AddScoped<ISourceCatalog<SubjectAction>, SubjectActionCatalog>()
            .AddScoped<ICatalog<SubjectAction>>(sp => sp.GetRequiredService<ISourceCatalog<SubjectAction>>())
            .AddScoped<ISubjectActionExecutor, DefaultSubjectActionExecutor>()
            .AddScoped<IActivityDispositionService, DefaultActivityDispositionService>()
            .AddScoped<IAutomatedActivityCompletionService, AutomatedActivityCompletionService>();

        services.AddSingleton<OmnichannelContentTypeProvider>();
        services.AddSingleton<IContentDefinitionEventHandler>(sp => sp.GetRequiredService<OmnichannelContentTypeProvider>());

        services.AddScoped<ISubjectFlowSettingsService, SubjectFlowSettingsService>();

        services.Configure<SubjectActionOptions>(options =>
        {
            options.AddActionType(OmnichannelConstants.ActionTypes.Finish, entry =>
            {
                entry.DisplayName = S["Finish"];
                entry.Description = S["Completes the task. No additional actions are taken."];
            });

            options.AddActionType(OmnichannelConstants.ActionTypes.TryAgain, entry =>
            {
                entry.DisplayName = S["Try Again"];
                entry.Description = S["Creates a retry activity with the same details and an incremented attempt count."];
            });

            options.AddActionType(OmnichannelConstants.ActionTypes.NewActivity, entry =>
            {
                entry.DisplayName = S["New Activity"];
                entry.Description = S["Creates a brand new activity, optionally targeting a different subject type."];
            });
        });

        services.Configure<ActivityBatchSourceOptions>(options =>
        {
            options.AddSource(ActivitySources.Manual, entry =>
            {
                entry.DisplayName = S["Manual"];
                entry.Description = S["Loads activities assigned to selected users for manual agent work."];
                entry.RequiresUserAssignment = true;
            });

            options.AddSource(ActivitySources.Automatic, entry =>
            {
                entry.DisplayName = S["Automatic"];
                entry.Description = S["Loads unassigned activities that AI automation processes through the configured subject flow."];
                entry.RequiresUserAssignment = false;
            });
        });

        // The sources this feature writes onto activities: manual work (the default, manual activity loads and
        // activities created by hand), automatic activity loads, and inbound activities logged by an agent. Other
        // features add the sources they produce, such as the dialer modes and callbacks.
        services.Configure<ActivitySourceOptions>(options =>
        {
            options.AddSource(ActivitySources.Manual, entry =>
            {
                entry.DisplayName = S["Manual"];
                entry.CanBeSetManually = true;
            });

            options.AddSource(ActivitySources.Automatic, entry =>
            {
                entry.DisplayName = S["Automatic"];
                entry.CanBeSetManually = true;
            });

            options.AddSource(ActivitySources.Inbound, entry =>
            {
                entry.DisplayName = S["Inbound"];
            });
        });

        // Subject flows and activity loads offer Phone and SMS whenever activities are enabled, so both channels can
        // occur on activities without any other feature. No feature creates email or chat activities.
        services.Configure<ActivityChannelOptions>(options =>
        {
            options.AddChannel(OmnichannelConstants.Channels.Phone, entry =>
            {
                entry.DisplayName = S["Phone"];
            });

            options.AddChannel(OmnichannelConstants.Channels.Sms, entry =>
            {
                entry.DisplayName = S["SMS"];
            });
        });

        // Permissions and their authorization handler belong here rather than with the screens: an API-only
        // deployment still has to authorize the requests it serves, and a permission that only exists when the
        // administration feature is on would fail closed for every headless caller.
        services.AddPermissionProvider<PermissionProvider>();
        // The handler declares its authorization-service dependency but resolves it lazily, because the service
        // is what runs the handler.
        services.AddScoped(sp => new Lazy<IAuthorizationService>(sp.GetRequiredService<IAuthorizationService>));
        services.AddScoped<IAuthorizationHandler, OmnichannelActivityAuthorizationHandler>();

        services
            .AddIndexProvider<OmnichannelContactIndexProvider>()
            .AddDataMigration<OmnichannelContactsMigrations>();

        services.AddDataMigration<ContactMethodMigrations>();

        services.AddContentPart<PhoneNumberInfoPart>();
        services.AddContentPart<EmailInfoPart>();
        services.AddContentPart<OmnichannelContactInfoPart>();

        services
            .AddIndexProvider<OmnichannelActivityIndexProvider>()
            .AddDataMigration<OmnichannelActivityIndexMigrations>();
    }

    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddSubjectDispositionActionsEndpoint();
    }
}
