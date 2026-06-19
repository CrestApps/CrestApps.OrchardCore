using CrestApps.OrchardCore.ContentAccessControl.Drivers;
using CrestApps.OrchardCore.ContentAccessControl.Handlers;
using CrestApps.OrchardCore.ContentAccessControl.Schemas;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentTypes.Editors;
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
    }
}

/// <summary>
/// Registers recipe schema contributors for Content Access Control.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class RecipesSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, RolePickerPartContentAccessControlSchemaDefinition>();
    }
}
