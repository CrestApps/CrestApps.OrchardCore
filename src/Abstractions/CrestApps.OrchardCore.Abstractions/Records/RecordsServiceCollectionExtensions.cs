using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Records;

/// <summary>
/// Registration helpers for record links.
/// </summary>
public static class RecordsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a source of records of one kind, and the <see cref="IRecordDirectory"/> that reads every source.
    /// </summary>
    /// <typeparam name="TSource">The source.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddRecordSource<TSource>(this IServiceCollection services)
        where TSource : class, IRecordSource
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRecordDirectory();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IRecordSource, TSource>());

        return services;
    }

    /// <summary>
    /// Registers the <see cref="IRecordDirectory"/> once, for a feature that links to records without keeping any.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddRecordDirectory(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IRecordDirectory, RecordDirectory>();

        return services;
    }
}
