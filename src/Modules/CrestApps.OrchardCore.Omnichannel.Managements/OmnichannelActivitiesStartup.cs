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

        services.AddSingleton<IBackgroundTask, AutomatedActivitiesProcessorBackgroundTask>();

        services
            .AddYesSqlDocumentCatalog<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>(collection: OmnichannelConstants.CollectionName)
            .AddScoped<ICatalog<OmnichannelActivityBatch>, OmnichannelActivityBatchCatalog>()
            .AddScoped<IOmnichannelActivityStore, OmnichannelActivityStore>()
            .AddScoped<IOmnichannelActivityManager, OmnichannelActivityManager>()
            .AddScoped<IOmnichannelChannelEndpointStore, OmnichannelChannelEndpointStore>()
            .AddScoped<IOmnichannelChannelEndpointManager, OmnichannelChannelEndpointManager>()
            .AddScoped<ICatalogEntryHandler<OmnichannelActivityBatch>, OmnichannelActivityBatchHandler>()
            .AddIndexProvider<OmnichannelActivityBatchIndexProvider>()
            .AddDataMigration<OmnichannelActivityBatchIndexMigrations>();

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

        // Permissions and their authorization handler belong here rather than with the screens: an API-only
        // deployment still has to authorize the requests it serves, and a permission that only exists when the
        // administration feature is on would fail closed for every headless caller.
        services.AddPermissionProvider<PermissionProvider>();
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
