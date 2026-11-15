using CrestApps.OrchardCore.KnowledgeBase.Drivers;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Migrations;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using CrestApps.OrchardCore.KnowledgeBase.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.KnowledgeBase;

/// <summary>
/// Registers the Knowledge Base.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<KnowledgeArticlePart>()
            .UseDisplayDriver<KnowledgeArticlePartDisplayDriver>();
        services.AddIndexProvider<KnowledgeArticleIndexProvider>();
        services.AddDataMigration<KnowledgeBaseMigrations>();
        services.AddScoped<KnowledgeArticleAccess>();
        services.AddScoped<KnowledgeArticleSearch>();
        services.AddScoped<IAuthorizationHandler, KnowledgeArticleAuthorizationHandler>();
        services.AddPermissionProvider<KnowledgeBasePermissionProvider>();
    }
}
