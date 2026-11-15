using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers audience rule sources.
/// </summary>
public static class UserAudienceServiceCollectionExtensions
{
    /// <summary>
    /// Registers a kind of audience rule.
    /// </summary>
    /// <typeparam name="TSource">The rule source.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddUserAudienceRuleSource<TSource>(this IServiceCollection services)
        where TSource : class, IUserAudienceRuleSource
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IUserAudienceRuleSource, TSource>());

        return services;
    }
}
