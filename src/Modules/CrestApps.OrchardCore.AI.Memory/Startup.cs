using CrestApps.Core.AI;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Memory.Drivers;
using CrestApps.OrchardCore.AI.Memory.Handlers;
using CrestApps.OrchardCore.AI.Memory.Migrations;
using CrestApps.OrchardCore.AI.Memory.Services;
using CrestApps.OrchardCore.AI.Services;
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

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddCoreAIMemory()
            .AddCoreAIMemoryStoresYesSql()
            .AddDataMigration<AIMemoryEntryMigrations>();

        services.AddTransient<IConfigureOptions<AIMemoryOptions>, AIMemoryOptionsConfiguration>()
                .AddTransient<IConfigureOptions<ChatInteractionMemoryOptions>, ChatInteractionMemoryOptionsConfiguration>()
                .AddScoped<IAIMemoryStore, DefaultAIMemoryStore>()
                .AddScoped<ICatalogEntryHandler<AIMemoryEntry>, AIMemoryEntryHandler>()
                .AddScoped<AIMemoryIndexingService>()
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

/// <summary>
/// Registers services and configuration for the ChatInteractions feature.
/// </summary>
[RequireFeatures(ChatInteractionsConstants.Feature.ChatInteractions)]
public sealed class ChatInteractionsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteDisplayDriver<ChatInteractionMemorySettingsDisplayDriver>();
    }
}

/// <summary>
/// Registers services and configuration for the UserMemory feature.
/// </summary>
[RequireFeatures("OrchardCore.Users")]
public sealed class UserMemoryStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDisplayDriver<User>, UserMemoryDisplayDriver>();
    }
}
