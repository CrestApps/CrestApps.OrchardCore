using CrestApps.OrchardCore.Learning.Certifications.Core.Requirements;
using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.YesSql.Core.Ledgers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Learning.Certifications.Core;

/// <summary>
/// Registers the core services of certification paths.
/// </summary>
public static class LearningCertificationsCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers paths, progress, the course requirement, the renewal queue, the upkeep and the observers of Learning
    /// and Skills. The assessment requirement is added by <see cref="AddLearningCertificationsAssessments"/>.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddLearningCertificationsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<CertificationPathStore>();
        services.TryAddScoped<CertificationPathProgressStore>();
        services.TryAddScoped<CertificateHolderResolver>();
        services.TryAddScoped<DefaultCertificationPathService>();
        services.TryAddScoped<ICertificationPathService>(provider => provider.GetRequiredService<DefaultCertificationPathService>());
        services.TryAddScoped<CertificationUpkeep>();
        services.AddCertificationRequirement<CourseCompletedRequirement>();
        services.AddLeasedWorkQueue<CertificationRenewalWork>(options =>
        {
            options.CollectionName = CertificationsConstants.RenewalWorkCollectionName;
            options.RemoveCompletedItems = true;
        });
        services.AddEnrollmentObserver<CertificationEnrollmentObserver>();
        services.TryAddScoped<EducationCreditTypeStore>();
        services.TryAddScoped<ExternalLearningSubmissionStore>();
        services.AddQuantityLedger<EducationCreditLedger>(options => options.CollectionName = CertificationsConstants.CreditLedgerCollectionName);
        services.TryAddScoped<IEducationCreditService, DefaultEducationCreditService>();
        services.AddCertificationRequirement<CreditsEarnedRequirement>();
        services.AddEnrollmentObserver<EducationCreditEnrollmentObserver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICertificationExpiryObserver, CertificationSkillsObserver>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICertificationRecordObserver, CertificationSkillsObserver>());

        return services;
    }

    /// <summary>
    /// Registers the observer that grants a completed course's competencies (Skills skills) to the learner.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddLearningCompetenciesCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddEnrollmentObserver<CompetencyEnrollmentObserver>();

        return services;
    }

    /// <summary>
    /// Registers the assessment requirement and the observer of passed quizzes.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddLearningCertificationsAssessments(this IServiceCollection services)
    {
        services.AddCertificationRequirement<AssessmentPassedRequirement>();
        services.AddSurveyResponseObserver<CertificationSurveyObserver>();

        return services;
    }
}
