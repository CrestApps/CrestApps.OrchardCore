using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Surveys.Core;

/// <summary>
/// Registers the collecting extras of Surveys: counters, evenly presented pools, quotas and invitations.
/// </summary>
public static class SurveysExtrasServiceCollectionExtensions
{
    /// <summary>
    /// Registers survey counters, evenly presented pools and quotas.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveysCollectingExtras(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<SurveyCounterStore>();
        services.RemoveAll<ISurveyPoolCounter>();
        services.AddScoped<ISurveyPoolCounter, SurveyPoolCounter>();
        services.TryAddScoped<ISurveyQuotaService, SurveyQuotaService>();
        services.AddSurveyAccessRule<QuotaRule>();

        return services;
    }

    /// <summary>
    /// Registers personal invitations and the personal links that replace the default no-op link factory.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveysInvitations(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<SurveyInvitationLinks>();
        services.TryAddScoped<SurveyInvitationService>();
        services.RemoveAll<ISurveyInvitationService>();
        services.AddScoped<ISurveyInvitationService>(provider => provider.GetRequiredService<SurveyInvitationService>());
        services.RemoveAll<ISurveyPersonalLinkFactory>();
        services.AddScoped<ISurveyPersonalLinkFactory, SurveyInvitationPersonalLinkFactory>();

        return services;
    }
}
