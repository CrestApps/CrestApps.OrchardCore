using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Makes a link to respond to a survey: a personal link for a user or an e-mail address and a context, or the public
/// link when no respondent is given. The link is written to the output for an e-mail or SMS task.
/// </summary>
public sealed class CreateSurveyLinkTask : SurveyTaskActivity<CreateSurveyLinkTask>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateSurveyLinkTask"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="expressionEvaluator">The Liquid evaluator.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public CreateSurveyLinkTask(
        IServiceProvider serviceProvider,
        IClock clock,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<CreateSurveyLinkTask> stringLocalizer)
        : base(expressionEvaluator, stringLocalizer)
    {
        _serviceProvider = serviceProvider;
        _clock = clock;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Create Survey Link"];

    /// <inheritdoc />
    protected override string DefaultOutputName => "SurveyLink";

    /// <inheritdoc />
    protected override async Task<string> RunAsync(Func<string, Task<string>> evaluate, IDictionary<string, object> output)
    {
        var surveyId = await evaluate(SurveyId);

        if (surveyId is null || _serviceProvider.GetService<ISurveyLinkService>() is not { } links)
        {
            return "Failed";
        }

        var userId = await evaluate(UserId);
        var email = await evaluate(Email);
        var contextKind = await evaluate(ContextKind);
        var contextId = await evaluate(ContextId);
        var days = ToInt(await evaluate(ExpiresInDays));
        RecordRef? context = contextKind is not null && contextId is not null ? new RecordRef(contextKind, contextId) : null;
        PartyRef? respondent = userId is not null ? new PartyRef(PartyKinds.User, userId) : null;

        var link = respondent is null && email is null && context is null
            ? await links.GetPublicLinkAsync(surveyId)
            : await links.CreateLinkAsync(surveyId, context, respondent, email, days > 0 ? _clock.UtcNow.AddDays(days) : null);

        output["SurveyId"] = surveyId;
        output["Link"] = link;

        return string.IsNullOrEmpty(link) ? "Failed" : "Done";
    }
}
