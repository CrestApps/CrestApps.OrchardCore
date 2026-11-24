using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Accounts.ViewModels;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Accounts.Workflows;

public sealed class AccountInvitationSentEvent : EventActivity
{
    public AccountInvitationSentEvent(IStringLocalizer<AccountInvitationSentEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    public override string Name => nameof(AccountInvitationSentEvent);

    public override LocalizedString Category => S["Accounts"];

    public override LocalizedString DisplayText => S["Account Invitation Sent"];

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext) => [new Outcome(S["Done"])];

    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext) => Outcome("Done");

    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext) => Outcome("Done");
}

public sealed class AccountInvitationAcceptedEvent : EventActivity
{
    public AccountInvitationAcceptedEvent(IStringLocalizer<AccountInvitationAcceptedEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    public override string Name => nameof(AccountInvitationAcceptedEvent);

    public override LocalizedString Category => S["Accounts"];

    public override LocalizedString DisplayText => S["Account Invitation Accepted"];

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext) => [new Outcome(S["Done"])];

    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext) => Outcome("Done");

    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext) => Outcome("Done");
}

/// <summary>
/// Invites an e-mail address to an account.
/// </summary>
public sealed class InviteToAccountTask : TaskActivity<InviteToAccountTask>
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IAccountInvitationService _invitations;

    public InviteToAccountTask(IWorkflowExpressionEvaluator expressionEvaluator, IAccountInvitationService invitations, IStringLocalizer<InviteToAccountTask> stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        _invitations = invitations;
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    public override LocalizedString Category => S["Accounts"];

    public override LocalizedString DisplayText => S["Invite to Account"];

    public string AccountId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public string Email
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public AccountMemberRole Role
    {
        get => GetProperty<AccountMemberRole>();
        set => SetProperty(value);
    }

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Failed"])];

    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var result = await _invitations.InviteAsync(new AccountInvitationRequest
        {
            AccountId = await EvaluateAsync(AccountId, workflowContext),
            Email = await EvaluateAsync(Email, workflowContext),
            Role = Role,
            InvitedBy = "Workflow",
        });

        workflowContext.LastResult = result.Succeeded ? result.Invitation.ItemId : result.Error;

        return Outcome(result.Succeeded ? "Done" : "Failed");
    }

    private async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
        => string.IsNullOrWhiteSpace(expression) ? null : (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();
}

public sealed class AccountInvitationSentEventDisplayDriver : ActivityDisplayDriver<AccountInvitationSentEvent>
{
}

public sealed class AccountInvitationAcceptedEventDisplayDriver : ActivityDisplayDriver<AccountInvitationAcceptedEvent>
{
}

public sealed class InviteToAccountTaskDisplayDriver : ActivityDisplayDriver<InviteToAccountTask, InviteToAccountTaskViewModel>
{
    protected override void EditActivity(InviteToAccountTask activity, InviteToAccountTaskViewModel model)
    {
        model.AccountId = activity.AccountId;
        model.Email = activity.Email;
        model.Role = activity.Role;
    }

    protected override void UpdateActivity(InviteToAccountTaskViewModel model, InviteToAccountTask activity)
    {
        activity.AccountId = model.AccountId?.Trim();
        activity.Email = model.Email?.Trim();
        activity.Role = model.Role;
    }
}

/// <summary>
/// Triggers the invitation workflow events after the request commits. The token is never part of the input.
/// </summary>
internal sealed class AccountInvitationWorkflowObserver : IAccountInvitationObserver
{
    public Task InvitedAsync(AccountInvitation invitation, string token, CancellationToken cancellationToken = default)
        => Trigger(nameof(AccountInvitationSentEvent), invitation);

    public Task AcceptedAsync(AccountInvitation invitation, CancellationToken cancellationToken = default)
        => Trigger(nameof(AccountInvitationAcceptedEvent), invitation);

    private static Task Trigger(string name, AccountInvitation invitation)
    {
        var input = new Dictionary<string, object>
        {
            ["InvitationId"] = invitation.ItemId,
            ["AccountId"] = invitation.AccountId,
            ["Email"] = invitation.Email,
            ["Role"] = invitation.Role.ToString(),
            ["UserId"] = invitation.AcceptedUserId,
            ["Source"] = invitation.Source,
            ["SourceId"] = invitation.SourceId,
        };

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(name, input, invitation.ItemId));

        return Task.CompletedTask;
    }
}

internal sealed class AccountInvitationSentEventSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "AccountInvitationSentEvent";

    protected override string Category => "Accounts";

    protected override string DisplayText => "Account Invitation Sent";

    protected override string Description => "Starts when a person is invited to an account";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context) => [];
}

internal sealed class AccountInvitationAcceptedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "AccountInvitationAcceptedEvent";

    protected override string Category => "Accounts";

    protected override string DisplayText => "Account Invitation Accepted";

    protected override string Description => "Starts when a person accepts an invitation and joins an account";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context) => [];
}

internal sealed class InviteToAccountTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "InviteToAccountTask";

    protected override string Category => "Accounts";

    protected override string DisplayText => "Invite to Account";

    protected override string Description => "Invites an e-mail address to an account";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["AccountId", "Email"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("AccountId", WorkflowActivitySchemaBuilders.String("The content item id of the account. A plain string that supports Liquid."));
        yield return ("Email", WorkflowActivitySchemaBuilders.String("The e-mail address to invite. A plain string that supports Liquid."));
        yield return ("Role", WorkflowActivitySchemaBuilders.String("Member (0) or Admin (1)."));
    }
}
