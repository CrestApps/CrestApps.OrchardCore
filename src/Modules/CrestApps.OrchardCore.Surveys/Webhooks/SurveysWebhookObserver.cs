using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Webhooks;

/// <summary>
/// Publishes the Surveys webhook events. Payloads of anonymous responses carry no identity, and their collector, version
/// and context follow the small-group rule of every other output; test responses publish nothing.
/// </summary>
internal sealed class SurveysWebhookObserver : ISurveyResponseObserver, ISurveyObserver
{
    private readonly ISurveyWebhookSender _sender;
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysWebhookObserver"/> class.
    /// </summary>
    /// <param name="sender">The webhook sender.</param>
    /// <param name="services">The services, for the small-group rule and the site's time zone of anonymous responses.</param>
    public SurveysWebhookObserver(ISurveyWebhookSender sender, IServiceProvider services)
    {
        _sender = sender;
        _services = services;
    }

    /// <inheritdoc />
    public async Task CompletedAsync(SurveyResponseEventContext context)
    {
        if (context?.Response is not { IsTest: false } response)
        {
            return;
        }

        _sender.Publish(SurveyWebhookEvents.ResponseCompleted, response.ItemId + ":completed", await PayloadAsync(context.Survey, response));
    }

    /// <inheritdoc />
    public async Task GradedAsync(SurveyResponseEventContext context)
    {
        if (context?.Response is not { IsTest: false } response)
        {
            return;
        }

        _sender.Publish(SurveyWebhookEvents.ResponseGraded, response.ItemId + ":graded:" + response.ScoreHistory.Count, await PayloadAsync(context.Survey, response));
    }

    /// <inheritdoc />
    public Task PublishedAsync(Survey survey, SurveyVersion version)
    {
        if (survey is null)
        {
            return Task.CompletedTask;
        }

        _sender.Publish(SurveyWebhookEvents.Published, survey.ItemId + ":published:" + version?.Number, SurveyWebhookPayloads.Published(survey, version));

        return Task.CompletedTask;
    }

    private async Task<Dictionary<string, object>> PayloadAsync(Survey survey, SurveyResponse response)
    {
        var (shows, timeZone) = await SurveyPayloadAnonymity.ReadAsync(_services, survey, response);

        return SurveyWebhookPayloads.Response(survey, response, shows, timeZone);
    }
}
