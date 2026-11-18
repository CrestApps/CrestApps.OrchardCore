using CrestApps.OrchardCore.ContentAccessControl.Drivers;
using CrestApps.OrchardCore.ContentAccessControl.Handlers;
using CrestApps.OrchardCore.ContentAccessControl.Indexes;
using CrestApps.OrchardCore.ContentAccessControl.Migrations;
using CrestApps.OrchardCore.ContentAccessControl.Schemas;
using CrestApps.OrchardCore.ContentAccessControl.Services;
using CrestApps.OrchardCore.Indexing;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContentAccessControl;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContentTypePartDefinitionDisplayDriver, RolePickerPartContentAccessControlSettingsDisplayDriver>()
            .AddScoped<IAuthorizationHandler, RoleBasedContentItemAuthorizationHandler>();

        services.AddScopedIndexProvider<ContentAccessRoleIndexProvider>();
        services.AddDataMigration<ContentAccessControlMigrations>();

        // So an AI knowledge base, which answers anyone allowed to chat, leaves restricted items out.
        services.AddIndexedDocumentVisibility<ContentAccessIndexedDocumentVisibility>();
    }
}

/// <summary>
/// Registers recipe schema contributors for Content Access Control.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, RolePickerPartContentAccessControlSchemaDefinition>();
    }
}
