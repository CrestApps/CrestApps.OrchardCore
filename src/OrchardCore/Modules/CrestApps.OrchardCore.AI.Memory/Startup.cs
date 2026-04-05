using CrestApps.AI;
using CrestApps.AI.Memory;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Memory.Drivers;
using CrestApps.OrchardCore.AI.Memory.Handlers;
using CrestApps.OrchardCore.AI.Memory.Indexes;
using CrestApps.OrchardCore.AI.Memory.Migrations;
using CrestApps.OrchardCore.AI.Memory.Services;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.AI.Memory;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddAIMemoryServices()
            .AddTransient<IConfigureOptions<AIMemoryOptions>, AIMemoryOptionsConfiguration>()
            .AddTransient<IConfigureOptions<ChatInteractionMemoryOptions>, ChatInteractionMemoryOptionsConfiguration>()
            .AddScoped<IAIMemoryStore, DefaultAIMemoryStore>()
            .AddScoped<ICatalogEntryHandler<AIMemoryEntry>, AIMemoryEntryHandler>()
            .AddScoped<AIMemoryIndexingService>()
            .AddIndexProvider<AIMemoryEntryIndexProvider>()
            .AddDataMigration<AIMemoryEntryMigrations>()
            .AddDataMigration<MemoryMetadataMigrations>()
            .AddDisplayDriver<AIProfile, AIProfileMemoryDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateMemoryDisplayDriver>()

            .AddDisplayDriver<IndexProfile, AIMemoryIndexProfileDisplayDriver>()
            .AddSiteDisplayDriver<AIMemorySettingsDisplayDriver>()
            .AddNavigationProvider<AISiteSettingsAdminMenu>();

        services.Configure<StoreCollectionOptions>(o => o.Collections.Add(MemoryConstants.CollectionName));
        services.AddIndexProfileHandler<AIMemoryIndexProfileHandler>();
    }
}

[RequireFeatures(ChatInteractionsConstants.Feature.ChatInteractions)]
public sealed class ChatInteractionsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {

        services.AddSiteDisplayDriver<ChatInteractionMemorySettingsDisplayDriver>();
    }
}

[RequireFeatures("OrchardCore.Users")]
public sealed class UserMemoryStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDisplayDriver<User>, UserMemoryDisplayDriver>();
    }
}
