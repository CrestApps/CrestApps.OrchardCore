using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Webhooks;

/// <summary>
/// Publishes the Surveys webhook events. Payloads of anonymous responses carry no identity; test responses publish
/// nothing.
/// </summary>
internal sealed class SurveysWebhookObserver : ISurveyResponseObserver, ISurveyObserver
{
    private readonly ISurveyWebhookSender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysWebhookObserver"/> class.
    /// </summary>
    /// <param name="sender">The webhook sender.</param>
    public SurveysWebhookObserver(ISurveyWebhookSender sender)
    {
        _sender = sender;
    }

    /// <inheritdoc />
    public Task CompletedAsync(SurveyResponseEventContext context)
    {
        if (context?.Response is not { IsTest: false } response)
        {
            return Task.CompletedTask;
        }

        _sender.Publish(SurveyWebhookEvents.ResponseCompleted, response.ItemId + ":completed", SurveyWebhookPayloads.Response(context.Survey, response));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task GradedAsync(SurveyResponseEventContext context)
    {
        if (context?.Response is not { IsTest: false } response)
        {
            return Task.CompletedTask;
        }

        _sender.Publish(SurveyWebhookEvents.ResponseGraded, response.ItemId + ":graded:" + response.ScoreHistory.Count, SurveyWebhookPayloads.Response(context.Survey, response));

        return Task.CompletedTask;
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
}
