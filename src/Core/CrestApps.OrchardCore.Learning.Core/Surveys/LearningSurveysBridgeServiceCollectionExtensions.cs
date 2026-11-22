using CrestApps.OrchardCore.Learning.Core.Surveys;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Localization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers how Learning uses Surveys: course items own surveys and learners answer them through their enrollment.
/// </summary>
public static class LearningSurveysBridgeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Learning survey service, the enrollment access rule, the access provider and grading scope for
    /// course staff, the observer that records course item results, the owner link and the owned survey handler.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddLearningSurveysBridge(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ILearningSurveyService, LearningSurveyService>();
        services.AddSurveyAccessRule<LearningEnrollmentAccessRule>();
        services.AddSurveyAccessProvider<LearningSurveyAccessProvider>();
        services.AddSurveyGradingScope<LearningSurveyGradingScope>();
        services.AddSurveyResponseObserver<LearningSurveyResultObserver>();
        services.AddSurveyOwnerLinkProvider<LearningSurveyOwnerLinkProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IContentHandler, LearningOwnedSurveyContentHandler>());
        services.AddSurveyContextKind(LearningSurveyContexts.EnrollmentKind, LocalizationSource.Create<LearningSurveyService>("Learning enrollment"));
        services.AddSurveyContextKind(LearningSurveyContexts.CourseKind, LocalizationSource.Create<LearningSurveyService>("Learning course (anonymous)"), identifiesRespondent: false);
        services.AddSurveyContextOwnershipCheck<LearningSurveyContextOwnershipCheck>();

        return services;
    }
}
