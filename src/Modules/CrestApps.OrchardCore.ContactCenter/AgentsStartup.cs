using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

[Feature(ContactCenterConstants.Feature.Agents)]
public sealed class AgentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IAgentProfileStore, AgentProfileStore>()
            .AddScoped<IAgentProfileManager, AgentProfileManager>()
            .AddScoped<IAgentStateReasonCodeStore, AgentStateReasonCodeStore>()
            .AddScoped<IAgentStateReasonCodeManager, AgentStateReasonCodeManager>();

        services
            .AddIndexProvider<AgentProfileIndexProvider>()
            .AddDataMigration<AgentProfileIndexMigrations>()
            .AddIndexProvider<AgentQueueMembershipIndexProvider>()
            .AddDataMigration<AgentQueueMembershipIndexMigrations>();

        services
            .AddScoped<ICatalogEntryHandler<AgentStateReasonCode>, AgentStateReasonCodeHandler>()
            .AddIndexProvider<AgentStateReasonCodeIndexProvider>()
            .AddDataMigration<AgentStateReasonCodeIndexMigrations>();

    }
}
