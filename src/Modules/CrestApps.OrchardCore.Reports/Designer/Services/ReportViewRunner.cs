using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Reports.Designer.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Runs a saved view as a data set: plans its query with the reader's access, runs it, and returns its result columns
/// as fields with date-times in UTC. Both a report that reads the view and the scheduled refresh of the view go
/// through here, so they read the same data. A view that reads itself, directly or through other views, is refused.
/// </summary>
public sealed class ReportViewRunner
{
    private const string StackKey = "ReportViews:Stack";
    private const int MaximumDepth = 8;

    private readonly Lazy<ReportQueryPlanner> _planner;
    private readonly Lazy<ReportQueryEngine> _engine;
    private readonly ReportExecutionContextFactory _contextFactory;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportViewRunner"/> class.
    /// </summary>
    /// <param name="planner">The query planner, resolved lazily because it reads the views data source.</param>
    /// <param name="engine">The query engine, resolved lazily because it reads the views data source.</param>
    /// <param name="contextFactory">The run context factory.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportViewRunner(
        Lazy<ReportQueryPlanner> planner,
        Lazy<ReportQueryEngine> engine,
        ReportExecutionContextFactory contextFactory,
        IStringLocalizer<ReportViewRunner> stringLocalizer)
    {
        _planner = planner;
        _engine = engine;
        _contextFactory = contextFactory;
        S = stringLocalizer;
    }

    /// <summary>
    /// Plans a view's query.
    /// </summary>
    /// <param name="view">The view.</param>
    /// <param name="context">The context of the reader, whose access plans the query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The plan, which may be invalid.</returns>
    public Task<ReportQueryPlan> PlanAsync(ReportView view, DataSourceContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(context);

        return _planner.Value.PlanAsync(view.Query, context, cancellationToken);
    }

    /// <summary>
    /// Plans a view's query and refuses a plan that cannot run.
    /// </summary>
    /// <param name="view">The view.</param>
    /// <param name="context">The context of the reader, whose access plans the query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The valid plan.</returns>
    /// <exception cref="ReportQueryException">The view cannot run.</exception>
    public async Task<ReportQueryPlan> PlanValidAsync(ReportView view, DataSourceContext context, CancellationToken cancellationToken = default)
    {
        var plan = await PlanAsync(view, context, cancellationToken);

        if (!plan.IsValid)
        {
            throw new ReportQueryException(S["The view '{0}' cannot run: {1}", view.DisplayText, string.Join(" ", plan.Errors)]);
        }

        return plan;
    }

    /// <summary>
    /// Runs a planned view query.
    /// </summary>
    /// <param name="plan">The plan of the view's query.</param>
    /// <param name="context">The context of the reader, whose access reads the data.</param>
    /// <param name="maxRows">The most rows to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The view's result as a data table, with date-times in UTC.</returns>
    public async Task<DataRowSet> ExecuteAsync(ReportQueryPlan plan, DataSourceContext context, int maxRows, CancellationToken cancellationToken = default)
    {
        return (await ExecuteCoreAsync(plan, context, maxRows, cancellationToken)).Table;
    }

    private async Task<ReportViewRunResult> ExecuteCoreAsync(ReportQueryPlan plan, DataSourceContext context, int maxRows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(context);

        var executionContext = await _contextFactory.CreateAsync(context.User, context);

        executionContext.Limits.MaxResultRows = Math.Max(1, maxRows);

        var result = await _engine.Value.ExecuteAsync(plan, executionContext, cancellationToken);
        var fields = result.Columns
            .Select(column => new DataField(column.Id, column.Label, column.DataType))
            .ToList();
        var rows = result.Rows.Select(row => ToUtc(row, result.Columns, executionContext)).ToList();

        return new ReportViewRunResult
        {
            Table = new DataRowSet
            {
                Fields = fields,
                Rows = rows,
                Truncated = result.Warnings.Count > 0 && rows.Count >= maxRows,
            },
            Warnings = result.Warnings.ToList(),
        };
    }

    /// <summary>
    /// Runs a view, refusing a view that reads itself or cannot run.
    /// </summary>
    /// <param name="view">The view.</param>
    /// <param name="context">The context of the reader, whose access reads the data.</param>
    /// <param name="maxRows">The most rows to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The view's result as a data table, with date-times in UTC, and the warnings of the run.</returns>
    /// <exception cref="ReportQueryException">The view reads itself or cannot run.</exception>
    public Task<ReportViewRunResult> RunAsync(ReportView view, DataSourceContext context, int maxRows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(context);

        return EnterAsync(view, context, async () =>
        {
            var plan = await PlanValidAsync(view, context, cancellationToken);

            return await ExecuteCoreAsync(plan, context, maxRows, cancellationToken);
        });
    }

    /// <summary>
    /// Streams the rows of a planned view's query, in batches, with date-times in UTC, without keeping them, such as
    /// for a report that reads the view.
    /// </summary>
    /// <param name="plan">The valid plan of the view's query.</param>
    /// <param name="context">The context of the reader, whose access runs the query.</param>
    /// <param name="maxRows">The most rows to read.</param>
    /// <param name="batchSize">The most rows of a batch.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The batches of rows.</returns>
    public async IAsyncEnumerable<DataBatch> ReadAsync(
        ReportQueryPlan plan,
        DataSourceContext context,
        int maxRows,
        int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(context);

        var executionContext = await _contextFactory.CreateAsync(context.User, context);
        var columns = ReportQueryEngine.DescribeColumns(plan);
        var fields = columns.Select(column => new DataField(column.Id, column.Label, column.DataType)).ToList();
        var size = batchSize > 0 ? batchSize : DataSourceQuery.DefaultBatchSize;
        var rows = new List<object[]>(size);
        var any = false;

        await foreach (var row in _engine.Value.ReadAsync(plan, executionContext, maxRows, warnings: null, cancellationToken).WithCancellation(cancellationToken))
        {
            rows.Add(ToUtc(row, columns, executionContext));

            if (rows.Count == size)
            {
                any = true;

                yield return new DataBatch(fields, rows);

                rows = new List<object[]>(size);
            }
        }

        // A view without rows still yields its fields.
        if (rows.Count > 0 || !any)
        {
            yield return new DataBatch(fields, rows);
        }
    }

    /// <summary>
    /// Runs an action while a view is being read, refusing a view that is already being read in the same run (a loop)
    /// or views nested too deeply.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="view">The view.</param>
    /// <param name="context">The context of the run, which tracks the views being read.</param>
    /// <param name="action">The action.</param>
    /// <returns>The action's result.</returns>
    /// <exception cref="ReportQueryException">The view reads itself, or views are nested too deeply.</exception>
    public async Task<T> EnterAsync<T>(ReportView view, DataSourceContext context, Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var entered = Enter(view, context);

        return await action();
    }

    /// <summary>
    /// Marks a view as being read until the returned object is disposed, refusing a view that is already being read in
    /// the same run (a loop) or views nested too deeply.
    /// </summary>
    /// <param name="view">The view.</param>
    /// <param name="context">The context of the run, which tracks the views being read.</param>
    /// <returns>Ends the read of the view when disposed.</returns>
    /// <exception cref="ReportQueryException">The view reads itself, or views are nested too deeply.</exception>
    public IDisposable Enter(ReportView view, DataSourceContext context)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Properties.TryGetValue(StackKey, out var value) || value is not HashSet<string> stack)
        {
            stack = new HashSet<string>(StringComparer.Ordinal);
            context.Properties[StackKey] = stack;
        }

        if (stack.Contains(view.ItemId))
        {
            throw new ReportQueryException(S["The view '{0}' reads itself through other views. Remove the loop.", view.DisplayText]);
        }

        if (stack.Count >= MaximumDepth)
        {
            throw new ReportQueryException(S["Views are nested more than {0} levels deep.", MaximumDepth]);
        }

        stack.Add(view.ItemId);

        return new ViewEntry(stack, view.ItemId);
    }

    /// <summary>
    /// Describes the fields a view exposes: one per result column of its plan, named after the column identifier.
    /// </summary>
    /// <param name="plan">The plan of the view's query.</param>
    /// <returns>The fields.</returns>
    public static List<DataField> DescribeFields(ReportQueryPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return plan.Columns
            .Select(column => new DataField(column.Definition.Id, column.Label, column.DataType)
            {
                IsIdentifier = column.Field.Kind == PlannedFieldKind.DataSetField &&
                    column.Definition.Aggregate == DataAggregate.None &&
                    column.Field.FieldName.EndsWith("Id", StringComparison.Ordinal),
            })
            .ToList();
    }

    // A row of the view's result with its date-times, which a run keeps in the tenant time zone, in UTC.
    private static object[] ToUtc(object[] source, IReadOnlyList<ReportResultColumn> columns, ReportQueryExecutionContext context)
    {
        var row = new object[source.Length];

        for (var index = 0; index < source.Length; index++)
        {
            row[index] = columns[index].DataType == DataFieldType.DateTime && source[index] is DateTime local
                ? context.ToUtc(local)
                : source[index];
        }

        return row;
    }

    private sealed class ViewEntry : IDisposable
    {
        private readonly HashSet<string> _stack;
        private readonly string _viewId;

        public ViewEntry(HashSet<string> stack, string viewId)
        {
            _stack = stack;
            _viewId = viewId;
        }

        public void Dispose() => _stack.Remove(_viewId);
    }
}

/// <summary>
/// The result of running a view.
/// </summary>
public sealed class ReportViewRunResult
{
    /// <summary>
    /// Gets or sets the view's result as a data table, with date-times in UTC.
    /// </summary>
    public DataRowSet Table { get; set; }

    /// <summary>
    /// Gets or sets the warnings of the run, such as a data set that was read only in part.
    /// </summary>
    public IList<string> Warnings { get; set; } = [];
}
