using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.MailingLists.Workflows;

/// <summary>
/// Reads the values of the mailing list workflow tasks.
/// </summary>
internal static class WorkflowValues
{
    /// <summary>
    /// Evaluates a Liquid value of a task.
    /// </summary>
    /// <param name="evaluator">The workflow expression evaluator.</param>
    /// <param name="expression">The value as the task holds it.</param>
    /// <param name="workflowContext">The workflow.</param>
    /// <returns>The trimmed value, or <see langword="null"/> when it is empty.</returns>
    public static async Task<string> EvaluateAsync(IWorkflowExpressionEvaluator evaluator, string expression, WorkflowExecutionContext workflowContext)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var value = await evaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null);

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Finds a list by its id or technical name.
    /// </summary>
    /// <param name="lists">The list manager.</param>
    /// <param name="value">The id or technical name.</param>
    /// <returns>The list, or <see langword="null"/>.</returns>
    public static async Task<MailingList> FindListAsync(IMailingListManager lists, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return await lists.FindByIdAsync(value) ?? await lists.FindByNameAsync(value);
    }
}
