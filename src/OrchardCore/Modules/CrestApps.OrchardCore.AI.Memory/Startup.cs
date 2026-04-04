using CrestApps.AI;
using CrestApps.AI.Memory;
using CrestApps.AI.Models;
using CrestApps.AI.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Memory.Drivers;
using CrestApps.OrchardCore.AI.Memory.Handlers;
using CrestApps.OrchardCore.AI.Memory.Indexes;
using CrestApps.OrchardCore.AI.Memory.Migrations;
using CrestApps.OrchardCore.AI.Memory.Models;
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
using OrchardCore.Settings;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.AI.Memory;

public sealed class Startup : StartupBase
{

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(o => o.Collections.Add(MemoryConstants.CollectionName));

        services
            .AddAIMemoryServices()
            .AddScoped<IOptions<AIMemoryOptions>>(sp =>
            {
                var options = sp.GetRequiredService<IOptionsSnapshot<AIMemoryOptions>>().Value.Clone();
                var settings = sp.GetRequiredService<ISiteService>().GetSettingsAsync<AIMemorySettings>().GetAwaiter().GetResult();
                options.IndexProfileName = string.IsNullOrWhiteSpace(settings.IndexProfileName) ? null : settings.IndexProfileName.Trim();
                options.TopN = settings.TopN;
                return Options.Create(options);
            })
            .AddScoped<IOptions<ChatInteractionMemoryOptions>>(sp =>
            {
                var options = sp.GetRequiredService<IOptionsSnapshot<ChatInteractionMemoryOptions>>().Value.Clone();
                var settings = sp.GetRequiredService<ISiteService>().GetSettingsAsync<ChatInteractionMemorySettings>().GetAwaiter().GetResult();
                options.EnableUserMemory = ChatInteractionMemoryOptions.FromSettings(settings).EnableUserMemory;
                return Options.Create(options);
            })
            .AddScoped<IAIMemoryStore, DefaultAIMemoryStore>()
            .AddScoped<ICatalogEntryHandler<AIMemoryEntry>, AIMemoryEntryHandler>()
            .AddScoped<AIMemoryIndexingService>()
            .AddIndexProvider<AIMemoryEntryIndexProvider>()
            .AddDataMigration<AIMemoryEntryMigrations>()
            .AddDisplayDriver<AIProfile, AIProfileMemoryDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateMemoryDisplayDriver>()

            .AddDisplayDriver<IndexProfile, AIMemoryIndexProfileDisplayDriver>()
            .AddSiteDisplayDriver<AIMemorySettingsDisplayDriver>()
            .AddNavigationProvider<AISiteSettingsAdminMenu>();

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
