using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers question types and attempt observers.
/// </summary>
public static class AssessmentsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a question type with its scorer.
    /// </summary>
    /// <typeparam name="TScorer">The scorer.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The question type.</param>
    /// <param name="configure">Sets the content type and name.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddQuestionType<TScorer>(this IServiceCollection services, string name, Action<QuestionTypeDescriptor> configure)
        where TScorer : class, IQuestionScorer
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);

        services.Configure<QuestionTypeOptions>(options =>
        {
            var descriptor = new QuestionTypeDescriptor(name);
            configure?.Invoke(descriptor);
            options.Add(descriptor);
        });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IQuestionScorer, TScorer>());

        return services;
    }

    /// <summary>
    /// Registers an observer of attempt changes.
    /// </summary>
    /// <typeparam name="TObserver">The observer.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAttemptObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, IAttemptObserver
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAttemptObserver, TObserver>());

        return services;
    }
}
