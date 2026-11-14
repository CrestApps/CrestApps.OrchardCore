using CrestApps.OrchardCore.Attachments;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Timeline.Core;
using CrestApps.OrchardCore.Timeline.Core.Indexes;
using CrestApps.OrchardCore.Timeline.Deployments;
using CrestApps.OrchardCore.Timeline.Migrations;
using CrestApps.OrchardCore.Timeline.Notifications;
using CrestApps.OrchardCore.Timeline.Recipes;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Timeline;

/// <summary>
/// Registers the Timeline feature: the entry store and index, the timeline service, the access service, the timeline
/// card and its endpoints.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(TimelineConstants.CollectionName));
        services.AddDataMigration<TimelineMigrations>()
            .AddIndexProvider<TimelineEntryIndexProvider>();

        services.AddTimelineCore();
        services.AddPermissionProvider<TimelinePermissionsProvider>();
    }
}

/// <summary>
/// Tells the users a note mentions with <c>@username</c>, through Orchard Core notifications.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Notifications)]
public sealed class NotificationsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTimelineEntryHandler<TimelineMentionHandler>();
    }
}

/// <summary>
/// Gives content items, such as accounts, contacts and leads, a timeline whose access follows the item's permissions.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Contents)]
public sealed class ContentsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTimelineAccessProvider<ContentItemTimelineAccess>();
        services.AddScoped<IContentHandler, ContentItemTimelineCleanup>();
    }
}

/// <summary>
/// Lets the timeline card take files when the Attachments feature is on: the files belong to their entry, and go with it.
/// </summary>
[RequireFeatures(AttachmentsConstants.Features.Attachments)]
public sealed class AttachmentsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAttachmentAccessProvider<TimelineEntryAttachmentAccess>();
        services.AddTimelineEntryHandler<TimelineEntryFilesCleanup>();
    }
}

/// <summary>
/// Registers the recipe step that imports timeline entries.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<TimelineEntriesRecipeStep>();
    }
}

/// <summary>
/// Registers the deployment step that exports timeline entries.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<TimelineEntriesDeploymentSource, TimelineEntriesDeploymentStep, TimelineEntriesDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the Timeline recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, TimelineEntriesRecipeStepSchema>();
        services.AddDeploymentStepSchema<TimelineEntriesDeploymentStepSchema>();
    }
}
