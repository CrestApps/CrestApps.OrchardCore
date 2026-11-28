using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Grants a respondent extra minutes or extra attempts on a survey, with a reason.
/// </summary>
public sealed class GrantSurveyOverrideTask : SurveyTaskActivity<GrantSurveyOverrideTask>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="GrantSurveyOverrideTask"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="expressionEvaluator">The Liquid evaluator.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public GrantSurveyOverrideTask(
        IServiceProvider serviceProvider,
        IClock clock,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<GrantSurveyOverrideTask> stringLocalizer)
        : base(expressionEvaluator, stringLocalizer)
    {
        _serviceProvider = serviceProvider;
        _clock = clock;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Grant Survey Accommodation"];

    /// <inheritdoc />
    protected override string DefaultOutputName => "SurveyOverride";

    /// <inheritdoc />
    protected override async Task<string> RunAsync(Func<string, Task<string>> evaluate, IDictionary<string, object> output)
    {
        var surveyId = await evaluate(SurveyId);
        var userId = await evaluate(UserId);
        var reason = await evaluate(Reason);

        if (surveyId is null || userId is null || reason is null || _serviceProvider.GetService<ISurveyOverrideService>() is not { } overrides)
        {
            return "Failed";
        }

        // The setting may name the user by id, user name or e-mail; one that names nobody fails the task.
        if ((userId = await SurveyWorkflowUsers.ResolveAsync(_serviceProvider, userId)) is null)
        {
            output["Error"] = "UnknownUser";

            return "Failed";
        }

        var entry = new SurveyOverride
        {
            ItemId = IdGenerator.GenerateId(),
            SurveyId = surveyId,
            UserId = userId,
            ExtraMinutes = Math.Max(0, ToInt(await evaluate(ExtraMinutes))),
            ExtraAttempts = Math.Max(0, ToInt(await evaluate(ExtraAttempts))),
            Reason = reason,
            Actor = SurveyWorkflowInput.WorkflowActor,
            CreatedUtc = _clock.UtcNow,
        };

        var error = await overrides.GrantAsync(entry);

        output["OverrideId"] = entry.ItemId;
        output["Error"] = error.ToString();

        return error == SurveyErrorCode.None ? "Done" : "Failed";
    }
}
