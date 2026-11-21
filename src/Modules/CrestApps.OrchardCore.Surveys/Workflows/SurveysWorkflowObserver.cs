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

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysWorkflowObserver"/> class.
    /// </summary>
    /// <param name="inputBuilder">The input builder.</param>
    /// <param name="trigger">The trigger.</param>
    public SurveysWorkflowObserver(
        SurveyWorkflowInputBuilder inputBuilder,
        ISurveyWorkflowTrigger trigger)
    {
        _inputBuilder = inputBuilder;
        _trigger = trigger;
    }

    /// <inheritdoc />
    public Task StartedAsync(SurveyResponseEventContext context)
        => Response(nameof(SurveyResponseStartedEvent), context);

    /// <inheritdoc />
    public Task PageCompletedAsync(SurveyResponseEventContext context)
        => Response(nameof(SurveyPageCompletedEvent), context);

    /// <inheritdoc />
    public Task CompletedAsync(SurveyResponseEventContext context)
    {
        if (context?.Response is null || context.Response.IsTest)
        {
            return Task.CompletedTask;
        }

        var input = _inputBuilder.ForResponse(context);

        _trigger.Trigger(nameof(SurveyResponseCompletedEvent), input, context.Response.ItemId);
        _trigger.Trigger(nameof(SurveyResponseMatchesConditionEvent), new Dictionary<string, object>(input, StringComparer.Ordinal), context.Response.ItemId);

        return Task.CompletedTask;
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
    public Task GradedAsync(SurveyResponseEventContext context)
    {
        if (context?.Response is null || context.Response.IsTest)
        {
            return Task.CompletedTask;
        }

        var input = _inputBuilder.ForResponse(context);

        _trigger.Trigger(nameof(SurveyResponseGradedEvent), input, context.Response.ItemId);

        if (context.Response.Passed is { } passed)
        {
            _trigger.Trigger(passed ? nameof(SurveyResponsePassedEvent) : nameof(SurveyResponseFailedEvent), new Dictionary<string, object>(input, StringComparer.Ordinal), context.Response.ItemId);
        }

        return Task.CompletedTask;
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

    private Task Response(string eventName, SurveyResponseEventContext context)
    {
        if (context?.Response is null || context.Response.IsTest)
        {
            return Task.CompletedTask;
        }

        _trigger.Trigger(eventName, _inputBuilder.ForResponse(context), context.Response.ItemId);

        return Task.CompletedTask;
    }
}
