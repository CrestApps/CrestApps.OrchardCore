using CrestApps.AI.Prompting.Extensions;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Memory.Drivers;
using CrestApps.OrchardCore.AI.Memory.Handlers;
using CrestApps.OrchardCore.AI.Memory.Indexes;
using CrestApps.OrchardCore.AI.Memory.Migrations;
using CrestApps.OrchardCore.AI.Memory.Services;
using CrestApps.OrchardCore.AI.Memory.Tools;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        services.AddAITemplatesFromAssembly(typeof(Startup).Assembly);

        services.Configure<StoreCollectionOptions>(o => o.Collections.Add(MemoryConstants.CollectionName));

        services
            .AddScoped<IAIMemoryStore, DefaultAIMemoryStore>()
            .AddScoped<IAIMemorySafetyService, DefaultAIMemorySafetyService>()
            .AddScoped<AIMemorySearchService>()
            .AddScoped<AIMemoryIndexingService>()
            .AddIndexProvider<AIMemoryEntryIndexProvider>()
            .AddDataMigration<AIMemoryEntryMigrations>()
            .AddDisplayDriver<AIProfile, AIProfileMemoryDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateMemoryDisplayDriver>()
            .AddDisplayDriver<IndexProfile, AIMemoryIndexProfileDisplayDriver>()
            .AddSiteDisplayDriver<AIMemorySettingsDisplayDriver>()
            .AddNavigationProvider<AISiteSettingsAdminMenu>();

        services.AddIndexProfileHandler<AIMemoryIndexProfileHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOrchestrationContextBuilderHandler, AIMemoryOrchestrationHandler>());

        services.AddAITool<SearchUserMemoriesTool>(SearchUserMemoriesTool.TheName)
            .WithTitle("Search User Memories")
            .WithDescription("Search the current authenticated user's long-term memory for relevant durable preferences, identity details, and background facts that were saved from prior conversations.")
            .WithPurpose(AIToolPurposes.Memory);

        services.AddAITool<ListUserMemoriesTool>(ListUserMemoriesTool.TheName)
            .WithTitle("List User Memories")
            .WithDescription("List the current authenticated user's saved long-term memories when you need to review what durable non-sensitive facts are already known about them.")
            .WithPurpose(AIToolPurposes.Memory);

        services.AddAITool<SaveUserMemoryTool>(SaveUserMemoryTool.TheName)
            .WithTitle("Save User Memory")
            .WithDescription("Create or update a long-term memory for the current authenticated user when they reveal a durable, non-sensitive fact that should persist across future conversations, even if they did not explicitly ask to save it.")
            .WithPurpose(AIToolPurposes.Memory);

        services.AddAITool<RemoveUserMemoryTool>(RemoveUserMemoryTool.TheName)
            .WithTitle("Remove User Memory")
            .WithDescription("Remove a previously saved long-term memory for the current authenticated user when the user asks to forget it or when the memory should no longer be retained.")
            .WithPurpose(AIToolPurposes.Memory);
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
