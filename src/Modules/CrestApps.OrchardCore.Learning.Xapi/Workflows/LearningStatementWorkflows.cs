using System.Globalization;
using CrestApps.OrchardCore.Learning.Xapi.Models;
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

namespace CrestApps.OrchardCore.Learning.Xapi.Workflows;

/// <summary>
/// Starts a workflow when the Learning Record Store stores a statement, for every verb or the verbs listed.
/// </summary>
public sealed class LearningStatementStoredEvent : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LearningStatementStoredEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LearningStatementStoredEvent(IStringLocalizer<LearningStatementStoredEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    /// <inheritdoc />
    public override string Name => nameof(LearningStatementStoredEvent);

    /// <inheritdoc />
    public override LocalizedString Category => S["Learning"];

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Learning Statement Stored"];

    /// <summary>
    /// Gets or sets the verb ids that start the workflow; empty for every verb.
    /// </summary>
    public IList<string> Verbs
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc />
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Verbs.Count == 0
            || (workflowContext.Input.TryGetValue("Verb", out var verb) && Verbs.Contains(verb?.ToString() ?? string.Empty));

    /// <inheritdoc />
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    /// <inheritdoc />
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

/// <summary>
/// The editor of the Learning Statement Stored event.
/// </summary>
public class LearningStatementStoredEventViewModel
{
    /// <summary>Gets or sets the verb ids, one a line.</summary>
    public string Verbs { get; set; }
}

/// <summary>
/// Edits the verbs of the Learning Statement Stored event.
/// </summary>
public sealed class LearningStatementStoredEventDisplayDriver : ActivityDisplayDriver<LearningStatementStoredEvent, LearningStatementStoredEventViewModel>
{
    /// <inheritdoc />
    protected override void EditActivity(LearningStatementStoredEvent activity, LearningStatementStoredEventViewModel model)
        => model.Verbs = string.Join(Environment.NewLine, activity.Verbs);

    /// <inheritdoc />
    protected override void UpdateActivity(LearningStatementStoredEventViewModel model, LearningStatementStoredEvent activity)
        => activity.Verbs = (model.Verbs ?? string.Empty)
            .Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();
}

/// <summary>
/// Starts the Learning Statement Stored workflows once statements are stored, with the statement id as the correlation
/// id. The input names the actor by account name only, never by e-mail address.
/// </summary>
internal sealed class LearningStatementWorkflowObserver : IXapiStatementObserver
{
    public Task StoredAsync(IReadOnlyList<XapiStatementRecord> records, XapiCredential credential)
    {
        foreach (var record in records)
        {
            var input = Input(record);

            ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(nameof(LearningStatementStoredEvent), input, record.StatementId));
        }

        return Task.CompletedTask;
    }

    internal static Dictionary<string, object> Input(XapiStatementRecord record)
        => new()
        {
            ["StatementId"] = record.StatementId,
            ["Verb"] = record.Verb,
            ["ObjectType"] = record.ObjectType,
            ["ObjectId"] = record.ObjectType is "Activity" or "StatementRef" ? record.ObjectId : null,
            ["Registration"] = record.Registration,
            ["ActorAccountName"] = record.ActorAccountName,
            ["ActorAccountHomePage"] = record.ActorAccountHomePage,
            ["ClientId"] = record.ClientId,
            ["Voided"] = record.Voided,
            ["StoredUtc"] = DateTime.SpecifyKind(record.StoredUtc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
        };
}

/// <summary>
/// Describes the Learning Statement Stored event to CrestApps Recipes.
/// </summary>
internal sealed class LearningStatementStoredEventSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => nameof(LearningStatementStoredEvent);

    protected override string Category => "Learning";

    protected override string DisplayText => "Learning Statement Stored";

    protected override string Description => "Starts when the Learning Record Store stores an xAPI statement";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Verbs", WorkflowActivitySchemaBuilders.StringArray("The verb ids that start the workflow. Empty for every verb."));
    }
}
