using System.Globalization;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Credentials.Workflows;

/// <summary>
/// Starts a workflow when a digital credential is awarded.
/// </summary>
public sealed class CredentialAwardedEvent : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialAwardedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public CredentialAwardedEvent(IStringLocalizer<CredentialAwardedEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    /// <inheritdoc/>
    public override string Name => nameof(CredentialAwardedEvent);

    /// <inheritdoc/>
    public override LocalizedString Category => S["Learning"];

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Credential Awarded"];

    /// <summary>
    /// Gets or sets the definitions whose awards start the workflow; empty for every definition.
    /// </summary>
    public IList<string> DefinitionIds
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc/>
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => DefinitionIds.Count == 0
            || (workflowContext.Input.TryGetValue("DefinitionId", out var definitionId) && DefinitionIds.Contains(definitionId?.ToString() ?? string.Empty));

    /// <inheritdoc/>
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    /// <inheritdoc/>
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

/// <summary>
/// Awards a digital credential to a person.
/// </summary>
public sealed class AwardCredentialTask : TaskActivity<AwardCredentialTask>
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly ICredentialAwardService _awards;

    /// <summary>
    /// Initializes a new instance of the <see cref="AwardCredentialTask"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The expression evaluator.</param>
    /// <param name="awards">The award service.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public AwardCredentialTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        ICredentialAwardService awards,
        IStringLocalizer<AwardCredentialTask> stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        _awards = awards;
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    /// <inheritdoc/>
    public override LocalizedString Category => S["Learning"];

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Award Credential"];

    /// <summary>
    /// Gets or sets the content item id of the credential definition; Liquid is allowed.
    /// </summary>
    public string DefinitionId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the user id of the recipient; Liquid is allowed.
    /// </summary>
    public string UserId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets what makes the award happen once, such as the id of the record that triggered the workflow; Liquid
    /// is allowed. Empty uses the workflow instance id.
    /// </summary>
    public string SourceId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Failed"])];

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var result = await _awards.AwardAsync(new CredentialAwardRequest
        {
            DefinitionId = await EvaluateAsync(DefinitionId, workflowContext),
            UserId = await EvaluateAsync(UserId, workflowContext),
            RuleKind = CredentialsConstants.RuleKinds.Workflow,
            SourceId = await EvaluateAsync(SourceId, workflowContext) ?? workflowContext.WorkflowId,
            Actor = "Workflow",
        });

        workflowContext.LastResult = result.Succeeded ? result.Award.ItemId : result.ErrorCode;

        return Outcome(result.Succeeded ? "Done" : "Failed");
    }

    private async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
        => string.IsNullOrWhiteSpace(expression)
            ? null
            : (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim() is { Length: > 0 } value ? value : null;
}

/// <summary>
/// The editor of the Credential Awarded event.
/// </summary>
public class CredentialAwardedEventViewModel
{
    /// <summary>
    /// Gets or sets the chosen definitions.
    /// </summary>
    public string[] DefinitionIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the published definitions.
    /// </summary>
    public IList<SelectListItem> DefinitionOptions { get; set; } = [];
}

/// <summary>
/// The editor of the Award Credential task.
/// </summary>
public class AwardCredentialTaskViewModel
{
    /// <summary>
    /// Gets or sets the definition expression.
    /// </summary>
    public string DefinitionId { get; set; }

    /// <summary>
    /// Gets or sets the recipient expression.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the source expression.
    /// </summary>
    public string SourceId { get; set; }
}

/// <summary>
/// Edits the Credential Awarded event.
/// </summary>
public sealed class CredentialAwardedEventDisplayDriver : ActivityDisplayDriver<CredentialAwardedEvent, CredentialAwardedEventViewModel>
{
    private readonly ICredentialDefinitionCatalog _definitions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialAwardedEventDisplayDriver"/> class.
    /// </summary>
    /// <param name="definitions">The definitions.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CredentialAwardedEventDisplayDriver(
        ICredentialDefinitionCatalog definitions,
        IStringLocalizer<CredentialAwardedEventDisplayDriver> stringLocalizer)
    {
        _definitions = definitions;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(CredentialAwardedEvent activity, BuildEditorContext context)
    {
        return Initialize<CredentialAwardedEventViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Trigger"], "10"));
    }

    /// <inheritdoc/>
    protected override async ValueTask EditActivityAsync(CredentialAwardedEvent activity, CredentialAwardedEventViewModel model)
    {
        model.DefinitionIds = activity.DefinitionIds.ToArray();
        model.DefinitionOptions = (await _definitions.ListAsync())
            .Select(definition => new SelectListItem(definition.Title, definition.DefinitionId, model.DefinitionIds.Contains(definition.DefinitionId)))
            .ToList();
    }

    /// <inheritdoc/>
    protected override void UpdateActivity(CredentialAwardedEventViewModel model, CredentialAwardedEvent activity)
        => activity.DefinitionIds = (model.DefinitionIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
}

/// <summary>
/// Edits the Award Credential task.
/// </summary>
public sealed class AwardCredentialTaskDisplayDriver : ActivityDisplayDriver<AwardCredentialTask, AwardCredentialTaskViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AwardCredentialTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AwardCredentialTaskDisplayDriver(IStringLocalizer<AwardCredentialTaskDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AwardCredentialTask activity, BuildEditorContext context)
    {
        return Initialize<AwardCredentialTaskViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Task"], "10"));
    }

    /// <inheritdoc/>
    protected override void EditActivity(AwardCredentialTask activity, AwardCredentialTaskViewModel model)
    {
        model.DefinitionId = activity.DefinitionId;
        model.UserId = activity.UserId;
        model.SourceId = activity.SourceId;
    }

    /// <inheritdoc/>
    protected override void UpdateActivity(AwardCredentialTaskViewModel model, AwardCredentialTask activity)
    {
        activity.DefinitionId = model.DefinitionId?.Trim();
        activity.UserId = model.UserId?.Trim();
        activity.SourceId = model.SourceId?.Trim();
    }
}

/// <summary>
/// Triggers the Credential Awarded event after an award is saved.
/// </summary>
internal sealed class CredentialWorkflowObserver : ICredentialAwardObserver
{
    /// <inheritdoc/>
    public Task AwardedAsync(CredentialAward award, CancellationToken cancellationToken = default)
    {
        var input = Input(award);

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(nameof(CredentialAwardedEvent), input, award.ItemId));

        return Task.CompletedTask;
    }

    /// <summary>
    /// The workflow input of an award. It never holds an e-mail address.
    /// </summary>
    /// <param name="award">The award.</param>
    /// <returns>The input.</returns>
    internal static Dictionary<string, object> Input(CredentialAward award)
        => new()
        {
            ["AwardId"] = award.ItemId,
            ["PublicId"] = award.PublicId,
            ["DefinitionId"] = award.DefinitionId,
            ["AchievementName"] = award.Achievement?.Name,
            ["UserId"] = award.RecipientUserId,
            ["RuleKind"] = award.RuleKind,
            ["SourceId"] = award.SourceId,
            ["IssuedUtc"] = DateTime.SpecifyKind(award.IssuedUtc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
            ["ExpiresUtc"] = award.ExpiresUtc is { } expires ? DateTime.SpecifyKind(expires, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture) : null,
        };
}

/// <summary>
/// The CrestApps Recipes schema of the Credential Awarded event.
/// </summary>
internal sealed class CredentialAwardedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc/>
    public override string Name => nameof(CredentialAwardedEvent);

    /// <inheritdoc/>
    protected override string Category => "Learning";

    /// <inheritdoc/>
    protected override string DisplayText => "Credential Awarded";

    /// <inheritdoc/>
    protected override string Description => "Starts when a digital credential is awarded";

    /// <inheritdoc/>
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("DefinitionIds", WorkflowActivitySchemaBuilders.StringArray("The content item ids of the credential definitions whose awards start the workflow. Empty for every definition."));
    }
}

/// <summary>
/// The CrestApps Recipes schema of the Award Credential task.
/// </summary>
internal sealed class AwardCredentialTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc/>
    public override string Name => nameof(AwardCredentialTask);

    /// <inheritdoc/>
    protected override string Category => "Learning";

    /// <inheritdoc/>
    protected override string DisplayText => "Award Credential";

    /// <inheritdoc/>
    protected override string Description => "Awards a digital credential to a person";

    /// <inheritdoc/>
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc/>
    protected override IEnumerable<string> RequiredProperties => ["DefinitionId", "UserId"];

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("DefinitionId", WorkflowActivitySchemaBuilders.String("The content item id of the published credential definition. A plain string that supports Liquid."));
        yield return ("UserId", WorkflowActivitySchemaBuilders.String("The user id of the recipient. A plain string that supports Liquid."));
        yield return ("SourceId", WorkflowActivitySchemaBuilders.String("What makes the award happen once. A plain string that supports Liquid; empty uses the workflow instance id."));
    }
}
