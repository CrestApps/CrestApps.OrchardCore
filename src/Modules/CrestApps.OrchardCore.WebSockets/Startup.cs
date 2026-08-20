using CrestApps.OrchardCore.WebSockets.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.WebSockets;

/// <summary>
/// Adds the ASP.NET Core WebSocket middleware to the tenant pipeline. The OrchardCore host does not enable
/// WebSockets on its own, so any feature that hosts a raw WebSocket endpoint depends on this feature to guarantee
/// the middleware runs before endpoint routing. This is intentionally the single place the middleware is enabled so
/// multiple WebSocket-hosting features do not each register it, and so reusable WebSocket services can be added here
/// later without touching those features.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // The default rendezvous registry is per-node in-memory. A distributed feature (for example one gated on the
        // Redis feature) can replace this registration so a provider callback that lands on another node still binds.
        services.AddSingleton<IWebSocketConnectionRegistry, InMemoryWebSocketConnectionRegistry>();

        // Bind the tenant configuration straight onto the framework WebSocketOptions the middleware consumes. Absent
        // values keep the framework defaults (a two-minute keep-alive interval and no origin restriction).
        services.Configure<WebSocketOptions>(_shellConfiguration.GetSection(WebSocketsConstants.ConfigurationSectionPath));
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.UseWebSockets(serviceProvider.GetRequiredService<IOptions<WebSocketOptions>>().Value);
    }
}
