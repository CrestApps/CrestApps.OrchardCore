using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers extensions of Surveys: question types, presets, access rules, observers and the other extension points.
/// </summary>
public static class SurveysServiceCollectionExtensions
{
    /// <summary>
    /// Registers a question type and its handler.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="services">The services.</param>
    /// <param name="name">The unique name of the type.</param>
    /// <param name="configure">Describes the type: display name, category, icon, capabilities and settings type.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveyControl<THandler>(this IServiceCollection services, string name, Action<SurveyControlDescriptor> configure = null)
        where THandler : class, ISurveyControlHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISurveyControlHandler, THandler>());
        services.Configure<SurveyControlOptions>(options =>
        {
            var descriptor = new SurveyControlDescriptor
            {
                Name = name,
                HandlerType = typeof(THandler),
            };

            configure?.Invoke(descriptor);
            descriptor.Name = name;
            descriptor.HandlerType = typeof(THandler);
            options.Controls[name] = descriptor;
        });

        return services;
    }

    /// <summary>
    /// Registers the scorer of a question type.
    /// </summary>
    /// <typeparam name="TScorer">The scorer type.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveyControlScorer<TScorer>(this IServiceCollection services)
        where TScorer : class, ISurveyControlScorer
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISurveyControlScorer, TScorer>());

        return services;
    }

    /// <summary>
    /// Registers the analyzer of a question type.
    /// </summary>
    /// <typeparam name="TAnalyzer">The analyzer type.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveyControlAnalyzer<TAnalyzer>(this IServiceCollection services)
        where TAnalyzer : class, ISurveyControlAnalyzer
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISurveyControlAnalyzer, TAnalyzer>());

        return services;
    }

    /// <summary>
    /// Registers a preset of a base question type, such as Net Promoter Score.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="name">The unique name of the preset.</param>
    /// <param name="baseControl">The name of the base type.</param>
    /// <param name="configure">Describes the preset and how it prepares a question.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveyControlPreset(this IServiceCollection services, string name, string baseControl, Action<SurveyControlPresetDescriptor> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(baseControl);

        services.Configure<SurveyControlOptions>(options =>
        {
            var descriptor = new SurveyControlPresetDescriptor
            {
                Name = name,
                BaseControl = baseControl,
            };

            configure?.Invoke(descriptor);
            descriptor.Name = name;
            descriptor.BaseControl = baseControl;
            options.Presets[name] = descriptor;
        });

        return services;
    }

    /// <summary>
    /// Registers an access rule.
    /// </summary>
    /// <typeparam name="TRule">The rule type.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveyAccessRule<TRule>(this IServiceCollection services)
        where TRule : class, ISurveyAccessRule
        => services.AddEnumerable<ISurveyAccessRule, TRule>();

    /// <summary>
    /// Registers a response observer.
    /// </summary>
    /// <typeparam name="TObserver">The observer type.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveyResponseObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, ISurveyResponseObserver
        => services.AddEnumerable<ISurveyResponseObserver, TObserver>();

    /// <summary>
    /// Registers a survey observer.
    /// </summary>
    /// <typeparam name="TObserver">The observer type.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveyObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, ISurveyObserver
        => services.AddEnumerable<ISurveyObserver, TObserver>();

    /// <summary>
    /// Registers a grading scope.
    /// </summary>
    /// <typeparam name="TScope">The scope type.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveyGradingScope<TScope>(this IServiceCollection services)
        where TScope : class, ISurveyGradingScope
        => services.AddEnumerable<ISurveyGradingScope, TScope>();

    /// <summary>
    /// Registers a question import format.
    /// </summary>
    /// <typeparam name="TFormat">The format type.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveyImportFormat<TFormat>(this IServiceCollection services)
        where TFormat : class, ISurveyImportFormat
        => services.AddEnumerable<ISurveyImportFormat, TFormat>();

    /// <summary>
    /// Registers an access provider.
    /// </summary>
    /// <typeparam name="TProvider">The provider type.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveyAccessProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ISurveyAccessProvider
        => services.AddEnumerable<ISurveyAccessProvider, TProvider>();

    /// <summary>
    /// Registers a provider of context condition sources.
    /// </summary>
    /// <typeparam name="TProvider">The provider type.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveyConditionSourceProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ISurveyConditionSourceProvider
        => services.AddEnumerable<ISurveyConditionSourceProvider, TProvider>();

    /// <summary>
    /// Registers a provider of fields of the SurveyResponses data set.
    /// </summary>
    /// <typeparam name="TProvider">The provider type.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveyResponseFieldProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ISurveyResponseFieldProvider
        => services.AddEnumerable<ISurveyResponseFieldProvider, TProvider>();

    /// <summary>
    /// Registers a provider of survey templates.
    /// </summary>
    /// <typeparam name="TProvider">The provider type.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveyTemplateProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ISurveyTemplateProvider
        => services.AddEnumerable<ISurveyTemplateProvider, TProvider>();

    /// <summary>
    /// Registers a contributor of definition checks.
    /// </summary>
    /// <typeparam name="TContributor">The contributor type.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveyDefinitionValidationContributor<TContributor>(this IServiceCollection services)
        where TContributor : class, ISurveyDefinitionValidationContributor
        => services.AddEnumerable<ISurveyDefinitionValidationContributor, TContributor>();

    /// <summary>
    /// Registers a kind of record responses may belong to.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="kind">The record kind.</param>
    /// <param name="displayName">The name shown.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveyContextKind(this IServiceCollection services, string kind, LocalizationSource displayName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(kind);

        services.Configure<SurveyContextKindOptions>(options => options.Kinds[kind] = new SurveyContextKindDescriptor
        {
            Kind = kind,
            DisplayName = displayName,
        });

        return services;
    }

    private static IServiceCollection AddEnumerable<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<TService, TImplementation>());

        return services;
    }
}
