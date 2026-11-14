using CrestApps.OrchardCore.AI.Chat.Core.Services;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Chat;

/// <summary>
/// Tells the admin chat widget which content item the user is editing, when Orchard Core's Contents feature is on.
/// </summary>
[Feature(AIConstants.Feature.ChatAdminWidget)]
[RequireFeatures(OrchardCoreFeatureIds.Contents)]
public sealed class AdminWidgetContentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IAIChatPageContextProvider, ContentItemPageContextProvider>();
    }
}
