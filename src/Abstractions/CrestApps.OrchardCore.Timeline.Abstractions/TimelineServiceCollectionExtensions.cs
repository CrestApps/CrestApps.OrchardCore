using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Timeline;

/// <summary>
/// Registration helpers for modules that build on the timeline.
/// </summary>
public static class TimelineServiceCollectionExtensions
{
    /// <summary>
    /// Registers who may read and write the timeline of one kind of record.
    /// </summary>
    /// <typeparam name="TProvider">The access provider.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTimelineAccessProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ITimelineAccessProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITimelineAccessProvider, TProvider>());

        return services;
    }

    /// <summary>
    /// Registers a handler that takes part in adding timeline entries.
    /// </summary>
    /// <typeparam name="THandler">The handler.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTimelineEntryHandler<THandler>(this IServiceCollection services)
        where THandler : class, ITimelineEntryHandler
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITimelineEntryHandler, THandler>());

        return services;
    }

    /// <summary>
    /// Lets the timeline of some content types take public comments, read by everyone who may view the item. Use it
    /// only for a type whose viewers are meant to read what staff write there.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="contentTypes">The content types.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AllowPublicTimelineEntries(this IServiceCollection services, params string[] contentTypes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(contentTypes);

        services.Configure<TimelineOptions>(options =>
        {
            foreach (var contentType in contentTypes.Where(type => !string.IsNullOrWhiteSpace(type)))
            {
                options.PublicContentTypes.Add(contentType.Trim());
            }
        });

        return services;
    }

    /// <summary>
    /// Adds a kind of entry the timeline card's composer offers, such as a reply on a module's own records.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="kind">The kind; one already offered is replaced.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTimelineComposerKind(this IServiceCollection services, TimelineComposerKind kind)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentException.ThrowIfNullOrEmpty(kind.Kind);

        services.Configure<TimelineOptions>(options =>
        {
            for (var index = options.ComposerKinds.Count - 1; index >= 0; index--)
            {
                if (string.Equals(options.ComposerKinds[index].Kind, kind.Kind, StringComparison.Ordinal))
                {
                    options.ComposerKinds.RemoveAt(index);
                }
            }

            options.ComposerKinds.Add(kind);
        });

        return services;
    }
}
