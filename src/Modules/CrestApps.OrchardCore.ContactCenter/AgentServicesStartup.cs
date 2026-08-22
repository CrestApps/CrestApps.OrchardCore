using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the <b>Contact Center Agent Services</b> feature: only the shared agent-profile directory —
/// the profile store, manager, and index, plus the storage collection they live in. This is the minimal set
/// that resolves an operator's agent identity, with no administration screens, presence, availability, reason
/// codes, or queue concepts. The full <see cref="ContactCenterConstants.Feature.Agents"/> feature and any
/// module that reuses agent identity (such as the SMS Workspace) depend on this feature, so agent identity is
/// available without pulling in the Agents and Work Distribution administration.
/// </summary>
[Feature(ContactCenterConstants.Feature.AgentServices)]
public sealed class AgentServicesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // The agent-profile directory lives in the Contact Center storage collection. Register it here so the
        // feature is self-contained when it is the only Contact Center feature enabled (the base Area feature
        // registers the same collection; the registration is idempotent).
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ContactCenterStorage.CollectionName));

        services
            .AddScoped<IAgentProfileStore, AgentProfileStore>()
            .AddScoped<IAgentProfileManager, AgentProfileManager>();

        services
            .AddIndexProvider<AgentProfileIndexProvider>()
            .AddDataMigration<AgentProfileIndexMigrations>();
    }
}
