using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Core.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the outreach engine and its extension points.
/// </summary>
public static class OutreachServiceCollectionExtensions
{
    /// <summary>
    /// Adds the outreach engine: the run and batch stores, the engine, the batch processor and the plain-text renderer.
    /// The Outreach feature calls it; a module that only adds sources, senders or policies does not.
    /// </summary>
    /// <param name="services">The tenant's services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddOutreachEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IOutreachRunStore, OutreachRunStore>();
        services.TryAddScoped<IOutreachBatchStore, OutreachBatchStore>();
        services.TryAddScoped<IOutreachEngine, OutreachEngine>();
        services.TryAddScoped<OutreachBatchProcessor>();
        services.TryAddScoped<OutreachQuietHours>();
        services.TryAddScoped<IOutreachScopeExecutor, OutreachScopeExecutor>();
        services.AddOutreachTemplateRenderer<TextOutreachTemplateRenderer>();
        services.AddOutreachRecipientPolicy<ConsentOutreachRecipientPolicy>();

        return services;
    }

    /// <summary>
    /// Registers an audience source.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <param name="services">The tenant's services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddOutreachAudienceSource<TSource>(this IServiceCollection services)
        where TSource : class, IOutreachAudienceSource
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutreachAudienceSource, TSource>());

        return services;
    }

    /// <summary>
    /// Registers a channel sender.
    /// </summary>
    /// <typeparam name="TSender">The sender type.</typeparam>
    /// <param name="services">The tenant's services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddOutreachChannelSender<TSender>(this IServiceCollection services)
        where TSender : class, IOutreachChannelSender
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutreachChannelSender, TSender>());

        return services;
    }

    /// <summary>
    /// Registers a template renderer.
    /// </summary>
    /// <typeparam name="TRenderer">The renderer type.</typeparam>
    /// <param name="services">The tenant's services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddOutreachTemplateRenderer<TRenderer>(this IServiceCollection services)
        where TRenderer : class, IOutreachTemplateRenderer
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutreachTemplateRenderer, TRenderer>());

        return services;
    }

    /// <summary>
    /// Registers a recipient policy, such as an opt-out or consent check.
    /// </summary>
    /// <typeparam name="TPolicy">The policy type.</typeparam>
    /// <param name="services">The tenant's services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddOutreachRecipientPolicy<TPolicy>(this IServiceCollection services)
        where TPolicy : class, IOutreachRecipientPolicy
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutreachRecipientPolicy, TPolicy>());

        return services;
    }

    /// <summary>
    /// Registers a run observer.
    /// </summary>
    /// <typeparam name="TObserver">The observer type.</typeparam>
    /// <param name="services">The tenant's services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddOutreachRunObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, IOutreachRunObserver
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutreachRunObserver, TObserver>());

        return services;
    }
}
