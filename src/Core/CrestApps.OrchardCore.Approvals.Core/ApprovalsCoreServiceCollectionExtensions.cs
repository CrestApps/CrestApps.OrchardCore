using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals.Core.Handlers;
using CrestApps.OrchardCore.Approvals.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Approvals.Core;

/// <summary>
/// Registers the approval services.
/// </summary>
public static class ApprovalsCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the request store, the policies, the rules and who the approver rules name.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddApprovalsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCatalogs().AddCatalogManagers();
        services.AddSourceAccessService();
        services.TryAddScoped<IApprovalRequestStore, ApprovalRequestStore>();
        services.TryAddScoped<IApprovalRequestManager, ApprovalRequestManager>();
        services.TryAddScoped<ICatalogManager<ApprovalRequest>>(serviceProvider => serviceProvider.GetRequiredService<IApprovalRequestManager>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<ApprovalRequest>, ApprovalRequestHandler>());
        services.TryAddScoped<IApproverDirectory, ApproverDirectory>();
        services.TryAddScoped<IApprovalCommitter, YesSqlApprovalCommitter>();
        services.TryAddScoped<IApprovalService, ApprovalService>();

        return services;
    }
}
