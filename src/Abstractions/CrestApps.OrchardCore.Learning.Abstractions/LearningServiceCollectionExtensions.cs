using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Learning;

/// <summary>
/// Registers the extension points of Learning: course item kinds, enrollment sources, release rules, completion
/// criteria and observers. The Learning core never names the modules that use them.
/// </summary>
public static class LearningServiceCollectionExtensions
{
    /// <summary>
    /// Registers a course item kind.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The stable name of the kind.</param>
    /// <param name="configure">Configures the kind.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCourseItemKind(this IServiceCollection services, string name, Action<CourseItemKindDescriptor> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);

        services.Configure<CourseItemKindOptions>(options => configure?.Invoke(options.GetOrAdd(name)));

        return services;
    }

    /// <summary>
    /// Registers a course item kind marked by a content part.
    /// </summary>
    /// <typeparam name="TPart">The content part that marks the kind.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The stable name of the kind.</param>
    /// <param name="configure">Configures the kind.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCourseItemKind<TPart>(this IServiceCollection services, string name, Action<CourseItemKindDescriptor> configure)
        where TPart : ContentPart
    {
        return services.AddCourseItemKind(name, descriptor =>
        {
            descriptor.PartName = typeof(TPart).Name;
            configure?.Invoke(descriptor);
        });
    }

    /// <summary>
    /// Registers an enrollment source, such as <c>Order</c> or <c>Licence</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The stable name of the source.</param>
    /// <param name="displayName">The name shown to people.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddEnrollmentSource(this IServiceCollection services, string name, LocalizationSource displayName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);

        services.Configure<EnrollmentSourceOptions>(options => options.Add(name, displayName));

        return services;
    }

    /// <summary>
    /// Registers something a cohort can be linked to so its people are enrolled, such as a course or a learning path.
    /// </summary>
    /// <typeparam name="TTarget">The target.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCohortLearningTarget<TTarget>(this IServiceCollection services)
        where TTarget : class, ICohortLearningTarget
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICohortLearningTarget, TTarget>());

        return services;
    }

    /// <summary>
    /// Registers a release rule.
    /// </summary>
    /// <typeparam name="TRule">The rule.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCourseItemReleaseRule<TRule>(this IServiceCollection services)
        where TRule : class, ICourseItemReleaseRule
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICourseItemReleaseRule, TRule>());

        return services;
    }

    /// <summary>
    /// Registers a course completion criterion.
    /// </summary>
    /// <typeparam name="TCriterion">The criterion.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCourseCompletionCriterion<TCriterion>(this IServiceCollection services)
        where TCriterion : class, ICourseCompletionCriterion
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICourseCompletionCriterion, TCriterion>());

        return services;
    }

    /// <summary>
    /// Registers an observer of enrollments.
    /// </summary>
    /// <typeparam name="TObserver">The observer.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddEnrollmentObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, IEnrollmentObserver
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IEnrollmentObserver, TObserver>());

        return services;
    }

    /// <summary>
    /// Registers a handler that hears when a learner starts and completes course items.
    /// </summary>
    /// <typeparam name="THandler">The handler.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCourseItemHandler<THandler>(this IServiceCollection services)
        where THandler : class, ICourseItemHandler
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICourseItemHandler, THandler>());

        return services;
    }
}
