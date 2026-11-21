using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Creates and sends a personal survey invitation to a user or an e-mail address.
/// </summary>
public sealed class SendSurveyInvitationTask : SurveyTaskActivity<SendSurveyInvitationTask>
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendSurveyInvitationTask"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services.</param>
    /// <param name="expressionEvaluator">The Liquid evaluator.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SendSurveyInvitationTask(
        IServiceProvider serviceProvider,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<SendSurveyInvitationTask> stringLocalizer)
        : base(expressionEvaluator, stringLocalizer)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Send Survey Invitation"];

    /// <inheritdoc />
    protected override string DefaultOutputName => "SurveyInvitation";

    /// <inheritdoc />
    protected override async Task<string> RunAsync(Func<string, Task<string>> evaluate, IDictionary<string, object> output)
    {
        var surveyId = await evaluate(SurveyId);
        var userId = await evaluate(UserId);
        var email = await evaluate(Email);

        if (surveyId is null || (userId is null && email is null) || _serviceProvider.GetService<ISurveyInvitationService>() is not { } invitations)
        {
            return "Failed";
        }

        var contextKind = await evaluate(ContextKind);
        var contextId = await evaluate(ContextId);
        var result = await invitations.InviteAsync(new SurveyInvitationRequest
        {
            SurveyId = surveyId,
            UserId = userId,
            Email = email,
            DisplayName = await evaluate(DisplayName),
            Context = contextKind is not null && contextId is not null ? new RecordRef(contextKind, contextId) : null,
        });

        output["SurveyId"] = surveyId;
        output["InvitationId"] = result.Invitation?.ItemId;
        output["Error"] = result.Error.ToString();

        return result.Succeeded ? "Done" : "Failed";
    }
}
