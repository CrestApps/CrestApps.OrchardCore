using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers what modules add to Pay Setup.
/// </summary>
public static class PaySetupServiceCollectionExtensions
{
    /// <summary>
    /// Adds a kind of pay code, offered in the pay code editor.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="name">The kind's stored name.</param>
    /// <param name="displayName">The kind's name.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPayCodeKind(this IServiceCollection services, string name, LocalizationSource displayName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);

        services.Configure<PayCodeKindOptions>(options => options.Kinds[name] = displayName);

        return services;
    }

    /// <summary>
    /// Adds a usage of pay schedules and pay codes, which keeps them from being deleted while used.
    /// </summary>
    /// <typeparam name="TUsage">The usage.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPaySetupUsage<TUsage>(this IServiceCollection services)
        where TUsage : class, IPaySetupUsage
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPaySetupUsage, TUsage>());

        return services;
    }

    /// <summary>
    /// Adds a source of pay inputs, read by pay runs and by the Pay Inputs data set.
    /// </summary>
    /// <typeparam name="TSource">The source.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPayInputSource<TSource>(this IServiceCollection services)
        where TSource : class, IPayInputSource
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayInputSource, TSource>());

        return services;
    }
}
