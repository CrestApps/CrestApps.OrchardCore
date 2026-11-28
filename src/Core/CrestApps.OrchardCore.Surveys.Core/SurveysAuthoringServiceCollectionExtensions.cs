using CrestApps.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Surveys.Core;

/// <summary>
/// Registers the authoring services of Surveys: the manager, the definition validator, differ and publisher, templates,
/// collectors, links and authorization.
/// </summary>
public static class SurveysAuthoringServiceCollectionExtensions
{
    /// <summary>
    /// Registers the authoring services.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveysAuthoring(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<SurveyManager>();
        services.TryAddScoped<ISurveyManager>(provider => provider.GetRequiredService<SurveyManager>());
        services.TryAddScoped<INamedCatalogManager<Survey>>(provider => provider.GetRequiredService<SurveyManager>());
        services.TryAddScoped<ICatalogManager<Survey>>(provider => provider.GetRequiredService<SurveyManager>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<Survey>, SurveyCatalogHandler>());
        services.TryAddScoped<SurveyResponseFiles>();
        services.TryAddScoped<SurveyDataPurger>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISurveyGradingScope, OwnSurveysGradingScope>());

        services.TryAddScoped<ISurveyResponseQueries, SurveyResponseQueries>();
        services.TryAddScoped<ISurveyFormulaValidator, NullSurveyFormulaValidator>();
        services.TryAddScoped<ISurveyDefinitionValidator, SurveyDefinitionValidator>();
        services.TryAddSingleton<ISurveyDefinitionDiffer, SurveyDefinitionDiffer>();
        services.TryAddScoped<SurveyBankDrawResolver>();
        services.TryAddScoped<ISurveyPublisher, SurveyPublisher>();
        services.TryAddScoped<SurveyDraftEditor>();
        services.TryAddScoped<SurveyListService>();
        services.TryAddScoped<SurveyIndexReader>();
        services.TryAddScoped<SurveyAccessScopes>();
        services.TryAddScoped<SurveyCollectorManager>();
        services.TryAddScoped<ISurveyAuthorizationService, SurveyAuthorizationService>();
        services.TryAddScoped<ISurveyPersonalLinkFactory, NullSurveyPersonalLinkFactory>();
        services.TryAddScoped<SurveyLinkService>();
        services.TryAddScoped<ISurveyLinkService>(provider => provider.GetRequiredService<SurveyLinkService>());
        services.AddSurveyTemplateProvider<SurveyTemplateProvider>();

        return services;
    }
}
