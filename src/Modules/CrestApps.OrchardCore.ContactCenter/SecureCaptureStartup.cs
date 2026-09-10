using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers agent-assisted secure data capture: persistence for capture sessions, the tokenization boundary, the
/// orchestration service, the expiry safety net, and the agent and customer endpoints.
/// </summary>
[Feature(ContactCenterConstants.Feature.SecureCapture)]
public sealed class SecureCaptureStartup : StartupBase
{
    private readonly IHostEnvironment _hostEnvironment;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureCaptureStartup"/> class.
    /// </summary>
    /// <param name="hostEnvironment">The host environment used to decide which default tokenization sink is safe to register.</param>
    public SecureCaptureStartup(IHostEnvironment hostEnvironment)
    {
        _hostEnvironment = hostEnvironment;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISecureCaptureSessionStore, SecureCaptureSessionStore>();
        services.AddScoped<ISecureCaptureSessionManager, SecureCaptureSessionManager>();
        services.AddScoped<ISecureCaptureService, SecureCaptureService>();

        // The default tokenization sink is chosen fail-closed. In development the in-tree masking sink lets the
        // feature be exercised without a vault; outside development the fail-closed sink refuses tokenization so a
        // deployment that enables secure capture without registering a PCI-DSS-compliant sink can never silently
        // accept sensitive data. Registered with TryAdd so a production deployment supplies its own sink.
        if (_hostEnvironment.IsDevelopment())
        {
            services.TryAddSingleton<ISecureCaptureTokenSink, MaskingSecureCaptureTokenSink>();
        }
        else
        {
            services.TryAddSingleton<ISecureCaptureTokenSink, UnconfiguredSecureCaptureTokenSink>();
        }

        services.AddScoped<IContactCenterRetentionPolicy, SecureCaptureSessionRetentionPolicy>();
        services.AddDataMigration<SecureCaptureSessionIndexMigrations>();
        services.AddIndexProvider<SecureCaptureSessionIndexProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, SecureCaptureExpiryBackgroundTask>());
        services.AddSiteDisplayDriver<SecureCaptureSettingsDisplayDriver>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddSecureCaptureEndpoints();
    }
}
