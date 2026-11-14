using CrestApps.OrchardCore.Approvals.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Approvals;

/// <summary>
/// Registers what other modules add to approvals.
/// </summary>
public static class ApprovalsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the handler of one subject kind, told when a request about it is decided.
    /// </summary>
    /// <typeparam name="THandler">The handler.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddApprovalSubjectHandler<THandler>(this IServiceCollection services)
        where THandler : class, IApprovalSubjectHandler
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IApprovalSubjectHandler, THandler>());

        return services;
    }

    /// <summary>
    /// Registers an observer of approval changes.
    /// </summary>
    /// <typeparam name="TObserver">The observer.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddApprovalObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, IApprovalObserver
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IApprovalObserver, TObserver>());

        return services;
    }
}
