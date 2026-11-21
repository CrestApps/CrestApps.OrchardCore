using CrestApps.OrchardCore.Surveys.Webhooks;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Publishes the Surveys webhook events.
/// </summary>
[RequireFeatures(WebhooksConstants.Feature.Webhooks)]
public sealed class WebhooksStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWebhookEvent(SurveyWebhookEvents.Published, "Survey published", "Surveys");
        services.AddWebhookEvent(SurveyWebhookEvents.ResponseCompleted, "Survey response completed", "Surveys");
        services.AddWebhookEvent(SurveyWebhookEvents.ResponseGraded, "Survey response graded", "Surveys");
        services.TryAddScoped<ISurveyWebhookSender, DeferredSurveyWebhookSender>();
        services.AddSurveyResponseObserver<SurveysWebhookObserver>();
        services.AddSurveyObserver<SurveysWebhookObserver>();
    }
}
