using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.SourceAccess;

/// <summary>
/// Registers who may see the records of a source kind.
/// </summary>
public static class SourceAccessServiceCollectionExtensions
{
    /// <summary>
    /// Registers who may see and act on the records of one source kind.
    /// </summary>
    /// <typeparam name="TAccess">The source access.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSourceAccess<TAccess>(this IServiceCollection services)
        where TAccess : class, ISourceAccess
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISourceAccess, TAccess>());

        return services;
    }
}
