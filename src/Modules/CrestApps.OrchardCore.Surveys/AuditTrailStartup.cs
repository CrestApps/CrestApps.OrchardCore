using CrestApps.Core.Services;
using CrestApps.OrchardCore.Surveys.AuditTrail;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Records survey changes, exports and score changes in the Audit Trail.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.AuditTrail)]
public sealed class AuditTrailStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, SurveysAuditTrailEvents>();
        services.RemoveAll<ISurveyAuditTrail>();
        services.AddScoped<ISurveyAuditTrail, AuditTrailSurveyAuditTrail>();
        services.AddSurveyResponseObserver<SurveysAuditTrailObserver>();
        services.AddSurveyObserver<SurveysAuditTrailObserver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<Survey>, SurveyCreatedAuditTrailHandler>());
    }
}
