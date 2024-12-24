using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Drivers;
using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.OpenAI;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIChatProfileServices()
            .AddNavigationProvider<OpenAIAdminMenu>();

        services.AddScoped<IDisplayDriver<AIChatProfile>, AIChatProfileDisplayDriver>();
    }
}

