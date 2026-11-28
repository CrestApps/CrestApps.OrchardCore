using CrestApps.OrchardCore.Learning.Credentials;
using CrestApps.OrchardCore.Learning.Credentials.Core.Rules;
using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the core services of Digital Credentials.
/// </summary>
public static class LearningCredentialsCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the award store and service, the signed credential, the status lists and the course completed rule.
    /// The module registers the shared signing keys and the definition catalog.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddLearningCredentialsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSigningKeySet(CredentialsConstants.KeySet, CredentialSigningKeys.Configure);
        services.TryAddScoped<CredentialAwardStore>();
        services.TryAddScoped<CredentialSigningKeys>();
        services.TryAddScoped<CredentialStatusLists>();
        services.TryAddScoped<CredentialVerifier>();
        services.TryAddScoped<CredentialAwardEraser>();
        services.TryAddScoped<CredentialIssuerContext>();
        services.TryAddScoped<CredentialRecipientResolver>();
        services.TryAddScoped<ICredentialAwardService, DefaultCredentialAwardService>();
        services.TryAddScoped<ICredentialAwardQueue, DeferredCredentialAwardQueue>();
        services.TryAddScoped<CredentialRuleTrigger>();
        services.TryAddScoped<CredentialSourceRevoker>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IEnrollmentObserver, CredentialEnrollmentObserver>());

        return services;
    }

    /// <summary>
    /// Registers the quiz passed rule, when Assessments is on.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddLearningCredentialsAssessments(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSurveyResponseObserver<CredentialSurveyObserver>();

        return services;
    }

    /// <summary>
    /// Registers the learning path completed rule, when Learning Paths is on.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddLearningCredentialsPrograms(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILearningPathObserver, CredentialLearningPathObserver>());

        return services;
    }

    /// <summary>
    /// Registers the certification issued rule, when Learning Certifications is on.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddLearningCredentialsCertifications(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICertificationRecordObserver, CredentialCertificationObserver>());

        return services;
    }
}
