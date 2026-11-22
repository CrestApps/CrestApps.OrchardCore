using CrestApps.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Surveys.Core;

/// <summary>
/// Registers the core services of Surveys.
/// </summary>
public static class SurveysCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the stores, the version cache and the control registry of Surveys.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveysStorage(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMemoryCache();

        services.TryAddScoped<SurveyStore>();
        services.TryAddScoped<SurveyAnonymitySecrets>();
        services.TryAddScoped<SurveyRespondentNames>();
        services.TryAddScoped<ICatalog<Survey>>(provider => provider.GetRequiredService<SurveyStore>());
        services.TryAddScoped<INamedCatalog<Survey>>(provider => provider.GetRequiredService<SurveyStore>());
        services.TryAddScoped<SurveyTemplateStore>();
        services.TryAddScoped<ICatalog<SurveyTemplate>>(provider => provider.GetRequiredService<SurveyTemplateStore>());
        services.TryAddScoped<INamedCatalog<SurveyTemplate>>(provider => provider.GetRequiredService<SurveyTemplateStore>());
        services.TryAddScoped<SurveyVersionStore>();
        services.TryAddScoped<ICatalog<SurveyVersion>>(provider => provider.GetRequiredService<SurveyVersionStore>());
        services.TryAddScoped<QuestionBankStore>();
        services.TryAddScoped<ICatalog<QuestionBank>>(provider => provider.GetRequiredService<QuestionBankStore>());
        services.TryAddScoped<INamedCatalog<QuestionBank>>(provider => provider.GetRequiredService<QuestionBankStore>());
        services.TryAddScoped<BankQuestionStore>();
        services.TryAddScoped<ICatalog<BankQuestion>>(provider => provider.GetRequiredService<BankQuestionStore>());
        services.TryAddScoped<SurveyCollectorStore>();
        services.TryAddScoped<ICatalog<SurveyCollector>>(provider => provider.GetRequiredService<SurveyCollectorStore>());
        services.TryAddScoped<SurveyResponseStore>();
        services.TryAddScoped<ICatalog<SurveyResponse>>(provider => provider.GetRequiredService<SurveyResponseStore>());
        services.TryAddScoped<SurveyParticipationStore>();
        services.TryAddScoped<ICatalog<SurveyParticipation>>(provider => provider.GetRequiredService<SurveyParticipationStore>());
        services.TryAddScoped<SurveyInvitationStore>();
        services.TryAddScoped<ICatalog<SurveyInvitation>>(provider => provider.GetRequiredService<SurveyInvitationStore>());
        services.TryAddScoped<SurveyOverrideStore>();
        services.TryAddScoped<ICatalog<SurveyOverride>>(provider => provider.GetRequiredService<SurveyOverrideStore>());

        services.TryAddScoped<ISurveyVersionCache, SurveyVersionCache>();
        services.TryAddScoped<ISurveyControlRegistry, SurveyControlRegistry>();

        return services;
    }
}
