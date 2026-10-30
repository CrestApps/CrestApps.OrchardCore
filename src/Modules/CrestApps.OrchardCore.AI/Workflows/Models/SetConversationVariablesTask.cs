using CrestApps.Core.AI.Chat;
using CrestApps.OrchardCore.AI.Core.Conversations;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// Sets AI conversation variables, which the system message reads as <c>{{ Variables.name }}</c> and tools read
/// through their context fills. In an <c>AI Conversation Starting</c> workflow they are what the conversation starts
/// with; in a workflow a tool runs, they change the live conversation.
/// </summary>
public sealed class SetConversationVariablesTask : TaskActivity<SetConversationVariablesTask>
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IAIChatSessionManager _chatSessionManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetConversationVariablesTask"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator, which renders the Liquid values.</param>
    /// <param name="chatSessionManager">The chat session manager, which saves the variables on a live conversation.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SetConversationVariablesTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        IAIChatSessionManager chatSessionManager,
        IStringLocalizer<SetConversationVariablesTask> stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        _chatSessionManager = chatSessionManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Set Conversation Variables"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["AI Conversations"];

    /// <summary>
    /// Gets or sets the variables, one <c>name: value</c> per line, rendered as a Liquid template.
    /// </summary>
    public string Variables
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["No conversation"])];

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var rendered = string.IsNullOrWhiteSpace(Variables)
            ? null
            : await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(Variables), workflowContext, null);

        var values = ParseLines(rendered);

        if (workflowContext.Input.TryGetValue(AIConversationWorkflowInput.StartingResult, out var value) &&
            value is AIConversationStartingResult starting)
        {
            foreach (var (name, variable) in values)
            {
                starting.Variables[name] = variable;
            }

            return Outcome("Done");
        }

        var conversation = AIConversationScope.Resolve();

        if (conversation is null)
        {
            return Outcome("No conversation");
        }

        if (conversation.SetVariables(values) && conversation.Session is not null)
        {
            await _chatSessionManager.SaveAsync(conversation.Session);
        }

        return Outcome("Done");
    }

    /// <summary>
    /// Reads <c>name: value</c> lines. Blank lines and lines without a name are skipped; a name with nothing after
    /// the colon clears the variable.
    /// </summary>
    /// <param name="text">The rendered text.</param>
    /// <returns>The variables in the order written.</returns>
    internal static List<KeyValuePair<string, string>> ParseLines(string text)
    {
        var values = new List<KeyValuePair<string, string>>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return values;
        }

        foreach (var line in text.Split('\n'))
        {
            var separator = line.IndexOf(':');

            if (separator <= 0)
            {
                continue;
            }

            var name = line[..separator].Trim();

            if (name.Length == 0)
            {
                continue;
            }

            var variable = line[(separator + 1)..].Trim();
            values.Add(new KeyValuePair<string, string>(name, variable.Length == 0 ? null : variable));
        }

        return values;
    }
}
