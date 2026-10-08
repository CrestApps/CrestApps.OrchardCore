using CrestApps.OrchardCore.TenantHierarchy.Core;
using CrestApps.OrchardCore.TenantHierarchy.Core.Guards;
using CrestApps.OrchardCore.TenantHierarchy.Core.Provisioning;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Removing;
using OrchardCore.Modules;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Adds the host-level part of the tenant hierarchy to an Orchard Core application.
/// </summary>
public static class TenantHierarchyOrchardCoreBuilderExtensions
{
    /// <summary>
    /// The order of the middleware that refuses script requests from child pages to their parent. It runs early.
    /// </summary>
    private const int FetchMetadataMiddlewareOrder = -10000;

    /// <summary>
    /// Installs the tenant hierarchy guards in every tenant: the <see cref="IShellHost"/> scope guard, the feature
    /// guard, the egress guard, the <c>__Host-</c> cookie names, the parent removal guard and the Fetch Metadata guard.
    /// Each guard reads the tenant's own shell settings and does nothing unless the tenant is part of a hierarchy.
    /// Call it from <c>Program.cs</c>, inside <c>AddOrchardCms(builder =&gt; builder.AddTenantHierarchy())</c>.
    /// </summary>
    /// <param name="builder">The Orchard Core builder.</param>
    public static OrchardCoreBuilder AddTenantHierarchy(this OrchardCoreBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.ApplicationServices;

        if (services.Any(descriptor => descriptor.ServiceType == typeof(TenantHierarchyHostMarker)))
        {
            return builder;
        }

        services.AddSingleton<TenantHierarchyHostMarker>();
        services.AddSingleton<IOptions<TenantHierarchyOptions>>(serviceProvider =>
        {
            var options = new TenantHierarchyOptions();
            serviceProvider.GetRequiredService<IConfiguration>().GetSection(TenantHierarchyOptions.SectionName).Bind(options);

            return Options.Options.Create(options);
        });

        DecorateShellHost(services);

        services.AddSingleton<IShellRemovingHandler, ParentRemovalHostHandler>();
        services.AddSingleton<IChildTenantDatabaseProvisioner, SqliteChildDatabaseProvisioner>();
        services.AddSingleton<IChildTenantDatabaseProvisioner, TablePrefixChildDatabaseProvisioner>();
        services.AddSingleton<IChildTenantDatabaseProvisioner, SqlServerChildDatabaseProvisioner>();
        services.AddSingleton<IChildTenantDatabaseProvisioner, PostgreSqlChildDatabaseProvisioner>();
        services.AddSingleton<ChildDatabaseProvisioning>();

        builder.ConfigureServices(tenantServices =>
        {
            tenantServices.AddScoped<IFeatureValidationProvider, TenantHierarchyFeatureValidationProvider>();
            tenantServices.AddSingleton<EgressGuard>();
            tenantServices.AddSingleton<IConfigureOptions<HttpClientFactoryOptions>, EgressGuardHttpClientFactoryOptionsSetup>();
            tenantServices.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, HostPrefixedCookieOptionsSetup>();
            tenantServices.AddSingleton<IPostConfigureOptions<AntiforgeryOptions>, HostPrefixedCookieOptionsSetup>();
        });

        // Registered last, so it runs first: Orchard Core runs the tenant removing handlers in reverse order, and the
        // handlers registered before it drop the tenant tables.
        builder.ConfigureServices(
            tenantServices => tenantServices.AddScoped<IModularTenantEvents, ParentRemovalTenantEvents>(),
            order: int.MaxValue);

        builder.Configure(app => app.UseMiddleware<ParentFetchMetadataMiddleware>(), FetchMetadataMiddlewareOrder);

        return builder;
    }

    private static void DecorateShellHost(IServiceCollection services)
    {
        var descriptor = services.LastOrDefault(candidate => candidate.ServiceType == typeof(IShellHost))
            ?? throw new InvalidOperationException("AddTenantHierarchy() must be called after Orchard Core is added.");

        services.Replace(ServiceDescriptor.Singleton<IShellHost>(serviceProvider =>
        {
            var inner = CreateInner(serviceProvider, descriptor);

            return new GuardedShellHost(inner, serviceProvider.GetRequiredService<ILogger<GuardedShellHost>>());
        }));
    }

    private static IShellHost CreateInner(IServiceProvider serviceProvider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is IShellHost instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return (IShellHost)descriptor.ImplementationFactory(serviceProvider);
        }

        return (IShellHost)ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType);
    }
}
