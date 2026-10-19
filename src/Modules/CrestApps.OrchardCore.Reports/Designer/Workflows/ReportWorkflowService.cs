using System.Net;
using System.Text;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Reports.Designer.Workflows;

/// <summary>
/// What the report workflow tasks share: finding the report a task names, reading the filter values it sets, running
/// the report with its owner's access, and turning the result into workflow output or an HTML table for an email.
/// Reports run this way do not raise the report events, so a workflow that runs a report cannot start itself again.
/// </summary>
public sealed class ReportWorkflowService
{
    /// <summary>
    /// The most rows a task puts in the workflow output or in an email.
    /// </summary>
    public const int MaxOutputRows = 1000;

    private readonly ReportDesignService _designService;
    private readonly ReportDesignRunner _runner;
    private readonly ReportDisplayValueResolver _displayValueResolver;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportWorkflowService"/> class.
    /// </summary>
    /// <param name="designService">The designed reports.</param>
    /// <param name="runner">The runner of designed reports.</param>
    /// <param name="displayValueResolver">The resolver of display values, such as user names.</param>
    /// <param name="expressionEvaluator">The evaluator of Liquid expressions.</param>
    public ReportWorkflowService(
        ReportDesignService designService,
        ReportDesignRunner runner,
        ReportDisplayValueResolver displayValueResolver,
        IWorkflowExpressionEvaluator expressionEvaluator)
    {
        _designService = designService;
        _runner = runner;
        _displayValueResolver = displayValueResolver;
        _expressionEvaluator = expressionEvaluator;
    }

    /// <summary>
    /// Evaluates a Liquid expression against the workflow.
    /// </summary>
    /// <param name="expression">The expression, or plain text.</param>
    /// <param name="workflowContext">The workflow.</param>
    /// <returns>The trimmed result, or <see langword="null"/> for an empty expression.</returns>
    public async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        return (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();
    }

    /// <summary>
    /// Finds the report a task names: the report its expression resolves to when it has one, otherwise the chosen one.
    /// </summary>
    /// <param name="task">The task.</param>
    /// <param name="workflowContext">The workflow.</param>
    /// <returns>The report, or <see langword="null"/> when there is none.</returns>
    public async Task<ReportDesign> FindReportAsync(IReportWorkflowTask task, WorkflowExecutionContext workflowContext)
    {
        ArgumentNullException.ThrowIfNull(task);

        var id = string.IsNullOrWhiteSpace(task.ReportIdExpression)
            ? task.ReportId
            : await EvaluateAsync(task.ReportIdExpression, workflowContext);

        return string.IsNullOrWhiteSpace(id) ? null : await _designService.FindAsync(id.Trim());
    }

    /// <summary>
    /// Reads the filter values a task sets: one <c>filter id = value</c> per line, after Liquid; a filter named on
    /// several lines gets all their values, and a filter with no value is left at its default.
    /// </summary>
    /// <param name="filters">The text of the task's filters.</param>
    /// <param name="workflowContext">The workflow.</param>
    /// <returns>The values by filter id.</returns>
    public async Task<IDictionary<string, IList<string>>> GetFilterValuesAsync(string filters, WorkflowExecutionContext workflowContext)
    {
        return ParseFilterValues(await EvaluateAsync(filters, workflowContext));
    }

    /// <summary>
    /// Parses filter values written as one <c>filter id = value</c> per line.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The values by filter id.</returns>
    public static IDictionary<string, IList<string>> ParseFilterValues(string text)
    {
        var values = new Dictionary<string, IList<string>>(StringComparer.Ordinal);

        foreach (var line in (text ?? string.Empty).Split('\n'))
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);

            if (separator <= 0)
            {
                continue;
            }

            var id = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            if (id.Length == 0 || value.Length == 0)
            {
                continue;
            }

            if (!values.TryGetValue(id, out var list))
            {
                values[id] = list = [];
            }

            list.Add(value);
        }

        return values;
    }

    /// <summary>
    /// Runs a report with its owner's access, with the display values of its document resolved.
    /// </summary>
    /// <param name="design">The report.</param>
    /// <param name="filterValues">The values of its filters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the run.</returns>
    public async Task<ReportRunResult> RunAsync(ReportDesign design, IDictionary<string, IList<string>> filterValues, CancellationToken cancellationToken = default)
    {
        var run = await _runner.RunAsync(design, filterValues, cancellationToken);

        if (run.Document is not null)
        {
            await _displayValueResolver.ResolveAsync(run.Document);
        }

        return run;
    }

    /// <summary>
    /// Describes a run as workflow output: the report, the number of rows, the column labels, and the first
    /// <see cref="MaxOutputRows"/> rows, each by column label.
    /// </summary>
    /// <param name="design">The report.</param>
    /// <param name="run">The result of its run.</param>
    /// <returns>The output, which Liquid reads as <c>Workflow.Output.Name.RowCount</c> and so on.</returns>
    public static Dictionary<string, object> ToOutput(ReportDesign design, ReportRunResult run)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(run);

        var columns = run.Columns ?? [];
        var rows = new List<Dictionary<string, object>>();

        foreach (var values in (run.Rows ?? []).Take(MaxOutputRows))
        {
            var row = new Dictionary<string, object>(StringComparer.Ordinal);

            for (var index = 0; index < columns.Count && index < values.Length; index++)
            {
                row[columns[index].Label ?? columns[index].Id] = values[index];
            }

            rows.Add(row);
        }

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["ReportId"] = design.ItemId,
            ["Title"] = design.DisplayText,
            ["RowCount"] = run.RowCount,
            ["Columns"] = columns.Select(column => column.Label ?? column.Id).ToArray(),
            ["Rows"] = rows,
            ["Errors"] = run.Errors.ToArray(),
        };
    }

    /// <summary>
    /// Renders the headline numbers and tables of a run as simple HTML for an email; charts are left out.
    /// </summary>
    /// <param name="run">The result of a run.</param>
    /// <returns>The HTML, or an empty string when there is nothing to show.</returns>
    public static string ToHtml(ReportRunResult run)
    {
        if (run?.Document is null)
        {
            return string.Empty;
        }

        var html = new StringBuilder();

        foreach (var section in run.Document.Sections)
        {
            if (section.Kind == ReportSectionKind.Metrics && section.Metrics.Count > 0)
            {
                html.Append("<table cellpadding=\"8\" style=\"border-collapse:collapse;margin:0 0 16px 0\"><tr>");

                foreach (var metric in section.Metrics)
                {
                    html.Append("<td style=\"border:1px solid #d0d7de\"><div style=\"font-size:12px;color:#57606a\">")
                        .Append(Encode(metric.Label))
                        .Append("</div><div style=\"font-size:20px;font-weight:600\">")
                        .Append(Encode(metric.Value))
                        .Append("</div></td>");
                }

                html.Append("</tr></table>");
            }
            else if (section.Kind == ReportSectionKind.Table && section.Columns.Count > 0)
            {
                if (!string.IsNullOrWhiteSpace(section.Title))
                {
                    html.Append("<h3 style=\"font-size:15px;margin:16px 0 8px 0\">").Append(Encode(section.Title)).Append("</h3>");
                }

                html.Append("<table cellpadding=\"6\" style=\"border-collapse:collapse;font-size:13px;margin:0 0 16px 0\"><thead><tr>");

                foreach (var column in section.Columns)
                {
                    html.Append("<th style=\"border:1px solid #d0d7de;background:#f6f8fa;text-align:")
                        .Append(column.Align == ReportColumnAlign.End ? "right" : "left")
                        .Append("\">")
                        .Append(Encode(column.Label))
                        .Append("</th>");
                }

                html.Append("</tr></thead><tbody>");

                foreach (var row in section.Rows.Take(MaxOutputRows))
                {
                    var emphasized = row.Kind != ReportRowKind.Detail;

                    html.Append(emphasized ? "<tr style=\"font-weight:600;background:#f6f8fa\">" : "<tr>");

                    for (var index = 0; index < section.Columns.Count; index++)
                    {
                        html.Append("<td style=\"border:1px solid #d0d7de;text-align:")
                            .Append(section.Columns[index].Align == ReportColumnAlign.End ? "right" : "left")
                            .Append("\">")
                            .Append(Encode(index < row.Cells.Count ? row.Cells[index] : null))
                            .Append("</td>");
                    }

                    html.Append("</tr>");
                }

                html.Append("</tbody></table>");
            }
        }

        return html.ToString();
    }

    private static string Encode(string text)
    {
        return WebUtility.HtmlEncode(text ?? string.Empty);
    }
}

/// <summary>
/// The settings every report workflow task has: the report it uses and the values of its filters.
/// </summary>
public interface IReportWorkflowTask
{
    /// <summary>
    /// Gets the id of the chosen report.
    /// </summary>
    string ReportId { get; }

    /// <summary>
    /// Gets a Liquid expression that resolves the report id, used instead of <see cref="ReportId"/> when set.
    /// </summary>
    string ReportIdExpression { get; }

    /// <summary>
    /// Gets the filter values, one <c>filter id = value</c> per line, with Liquid.
    /// </summary>
    string Filters { get; }
}
