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
    /// Registers an approval type and its handler, such as a pay run, a ticket or a time sheet. Registering a name
    /// again replaces it.
    /// </summary>
    /// <typeparam name="THandler">The handler, derived from <see cref="ApprovalTypeHandlerBase"/>.</typeparam>
    /// <param name="services">The services.</param>
    /// <param name="name">The type's stored name, a request's <c>SubjectKind</c>; it never changes.</param>
    /// <param name="configure">Sets what people see.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddApprovalType<THandler>(this IServiceCollection services, string name, Action<ApprovalType> configure = null)
        where THandler : class, IApprovalTypeHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        services.TryAddScoped<THandler>();

        return services.Configure<ApprovalTypeOptions>(options =>
        {
            var type = new ApprovalType { Name = name, HandlerType = typeof(THandler) };

            configure?.Invoke(type);
            type.Name = name;
            options.Types[name] = type;
        });
    }

    /// <summary>
    /// Registers an approver rule provider, a way policies name approvers.
    /// </summary>
    /// <typeparam name="TProvider">The provider.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddApproverRuleProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IApproverRuleProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IApproverRuleProvider, TProvider>());

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
