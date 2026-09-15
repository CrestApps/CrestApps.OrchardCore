using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Realtime;
using CrestApps.Core.AI.Realtime;
using CrestApps.OrchardCore.AI.Chat.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Chat.Core;

/// <summary>
/// Provides extension methods for registering AI chat services shared by the chat modules.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Sources the realtime TURN servers from Cloudflare Realtime, reading the TURN token from the tenant's
    /// own configuration rather than the host's.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <remarks>
    /// Registering this is safe before a token exists: while the tenant has none, the Cloudflare provider
    /// defers to the STUN and TURN servers configured on <see cref="RealtimeTransportOptions"/>, so a tenant
    /// running its own coturn — or none at all — is unaffected until it opts in.
    /// </remarks>
    public static IServiceCollection AddTenantCloudflareRealtimeTurn(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCloudflareRealtimeTurn();

        // The core registration binds the host configuration, which under Orchard Core no tenant can reach.
        // Post-configure instead, which applies after every Configure regardless of module load order.
        services.AddTransient<IPostConfigureOptions<CloudflareTurnOptions>, CloudflareTurnOptionsConfiguration>();

        return services;
    }
}
