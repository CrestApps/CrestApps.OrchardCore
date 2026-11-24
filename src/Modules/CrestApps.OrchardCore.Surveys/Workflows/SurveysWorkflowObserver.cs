using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Turns what happens to surveys and responses into workflow events. Test responses start no workflow.
/// </summary>
internal sealed class SurveysWorkflowObserver : ISurveyResponseObserver, ISurveyObserver
{
    private readonly SurveyWorkflowInputBuilder _inputBuilder;
    private readonly ISurveyWorkflowTrigger _trigger;
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysWorkflowObserver"/> class.
    /// </summary>
    /// <param name="inputBuilder">The input builder.</param>
    /// <param name="trigger">The trigger.</param>
    /// <param name="services">The services, for the small-group rule and the site's time zone of anonymous responses.</param>
    public SurveysWorkflowObserver(
        SurveyWorkflowInputBuilder inputBuilder,
        ISurveyWorkflowTrigger trigger,
        IServiceProvider services)
    {
        _inputBuilder = inputBuilder;
        _trigger = trigger;
        _services = services;
    }

    /// <inheritdoc />
    public Task StartedAsync(SurveyResponseEventContext context)
        => Response(nameof(SurveyResponseStartedEvent), context);

    /// <inheritdoc />
    public Task PageCompletedAsync(SurveyResponseEventContext context)
        => Response(nameof(SurveyPageCompletedEvent), context);

    /// <inheritdoc />
    public async Task CompletedAsync(SurveyResponseEventContext context)
    {
        if (context?.Response is null || context.Response.IsTest)
        {
            return;
        }

        var input = await InputAsync(context);

        _trigger.Trigger(nameof(SurveyResponseCompletedEvent), input, context.Response.ItemId);
        _trigger.Trigger(nameof(SurveyResponseMatchesConditionEvent), new Dictionary<string, object>(input, StringComparer.Ordinal), context.Response.ItemId);
    }

    /// <inheritdoc />
    public Task ScreenedOutAsync(SurveyResponseEventContext context)
        => Response(nameof(SurveyResponseScreenedOutEvent), context);

    /// <inheritdoc />
    public Task AbandonedAsync(SurveyResponseEventContext context)
        => Response(nameof(SurveyResponseAbandonedEvent), context);

    /// <inheritdoc />
    public Task NeedsGradingAsync(SurveyResponseEventContext context)
        => Response(nameof(SurveyResponseNeedsGradingEvent), context);

    /// <inheritdoc />
    public async Task GradedAsync(SurveyResponseEventContext context)
    {
        if (context?.Response is null || context.Response.IsTest)
        {
            return;
        }

        var input = await InputAsync(context);

        _trigger.Trigger(nameof(SurveyResponseGradedEvent), input, context.Response.ItemId);

        if (context.Response.Passed is { } passed)
        {
            _trigger.Trigger(passed ? nameof(SurveyResponsePassedEvent) : nameof(SurveyResponseFailedEvent), new Dictionary<string, object>(input, StringComparer.Ordinal), context.Response.ItemId);
        }
    }

    /// <inheritdoc />
    public Task ScoreChangedAsync(SurveyResponseEventContext context)
        => Response(nameof(SurveyResponseScoreChangedEvent), context);

    /// <inheritdoc />
    public Task PublishedAsync(Survey survey, SurveyVersion version)
    {
        if (survey is null)
        {
            return Task.CompletedTask;
        }

        var input = SurveyWorkflowInputBuilder.ForSurvey(survey);

        input["VersionNumber"] = version?.Number;
        input["Notes"] = version?.Notes;
        _trigger.Trigger(nameof(SurveyPublishedEvent), input, survey.ItemId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClosedAsync(Survey survey)
    {
        if (survey is null)
        {
            return Task.CompletedTask;
        }

        _trigger.Trigger(nameof(SurveyClosedEvent), SurveyWorkflowInputBuilder.ForSurvey(survey), survey.ItemId);

        return Task.CompletedTask;
    }

    private async Task Response(string eventName, SurveyResponseEventContext context)
    {
        if (context?.Response is null || context.Response.IsTest)
        {
            return;
        }

        _trigger.Trigger(eventName, await InputAsync(context), context.Response.ItemId);
    }

    // The input of a response event: an anonymous response's collector, version and context follow the small-group rule
    // of every other output, and its day is the site's.
    private async Task<Dictionary<string, object>> InputAsync(SurveyResponseEventContext context)
    {
        var (shows, timeZone) = await SurveyPayloadAnonymity.ReadAsync(_services, context.Survey, context.Response);

        return _inputBuilder.ForResponse(context, shows, timeZone);
    }
}
