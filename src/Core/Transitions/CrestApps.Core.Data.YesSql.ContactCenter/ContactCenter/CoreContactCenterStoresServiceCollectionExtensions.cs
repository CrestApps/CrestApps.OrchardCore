using CrestApps.Core.Builders;
using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.Data.YesSql.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.Core.Data.YesSql.ContactCenter;

/// <summary>
/// Registers the YesSql persistence behind the Contact Center services.
/// </summary>
/// <remarks>
/// These are kept apart from the services that read through them, so a host that stores its Contact Center
/// data somewhere else registers its own implementations of the same contracts and calls nothing here. The
/// builder method is the one a host composing the suite calls; it is sugar over the
/// <c>AddCore*StoresYesSql</c> method beneath it, which is what a host registering services elsewhere calls
/// instead.
/// <para>
/// One method per feature rather than one for the whole component, because a store is not free to register
/// speculatively: several of them come with a retention policy, and a policy registered for a feature the
/// host did not enable is a purge aimed at a table that was never created.
/// </para>
/// </remarks>
public static class CoreContactCenterStoresServiceCollectionExtensions
{
    /// <summary>
    /// Registers the YesSql store behind the agent directory.
    /// </summary>
    /// <param name="builder">The contact centre builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static CrestAppsContactCenterBuilder AddAgentDirectoryYesSqlStores(this CrestAppsContactCenterBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddCoreContactCenterAgentDirectoryStoresYesSql();

        return builder;
    }

    /// <summary>
    /// Registers the YesSql store the agent directory reads through.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreContactCenterAgentDirectoryStoresYesSql(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IAgentProfileStore, AgentProfileStore>();

        return services;
    }
}
