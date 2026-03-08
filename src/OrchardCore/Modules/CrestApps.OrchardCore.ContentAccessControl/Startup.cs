using CrestApps.OrchardCore.ContentAccessControl.Drivers;
using CrestApps.OrchardCore.ContentAccessControl.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContentAccessControl;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContentTypePartDefinitionDisplayDriver, RolePickerPartContentAccessControlSettingsDisplayDriver>()
            .AddScoped<IAuthorizationHandler, RoleBasedContentItemAuthorizationHandler>();
    }
}
