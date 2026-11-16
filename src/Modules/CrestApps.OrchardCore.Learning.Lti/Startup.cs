using CrestApps.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Core.Indexes;
using CrestApps.OrchardCore.Learning.Lti.Drivers;
using CrestApps.OrchardCore.Learning.Lti.Handlers;
using CrestApps.OrchardCore.Learning.Lti.Migrations;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Lti.Navigation;
using CrestApps.OrchardCore.Learning.Lti.Services;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Lti;

/// <summary>
/// Registers External Tools: the tool records and their admin pages, the External tool course item with its editor and
/// learner panel, the launch, the platform key on the shared signing keys, and the permission.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(LtiConstants.CollectionName));
        services.AddDataMigration<LtiMigrations>()
            .AddIndexProvider<ExternalToolIndexProvider>()
            .AddIndexProvider<LtiLineItemIndexProvider>()
            .AddIndexProvider<LtiScoreIndexProvider>();

        services.AddSigningKeys();
        services.AddLearningLtiCore();
        services.AddScoped<ICatalogEntryHandler<ExternalTool>, ExternalToolHandler>();
        services.AddScoped<ExternalToolClients>();
        services.AddScoped<LtiDeepLinkItemWriter>();
        services.AddDisplayDriver<ExternalTool, ExternalToolDisplayDriver>();

        services.AddContentPart<ExternalToolItemPart>()
            .UseDisplayDriver<ExternalToolItemPartDisplayDriver>()
            .UseDisplayDriver<ExternalToolItemPartLearnerDisplayDriver>();

        services.AddContentPart<CoursePart>()
            .UseDisplayDriver<CourseExternalToolsDisplayDriver>();

        services.AddPermissionProvider<LtiPermissionProvider>();
        services.AddNavigationProvider<LtiAdminMenu>();
    }
}
