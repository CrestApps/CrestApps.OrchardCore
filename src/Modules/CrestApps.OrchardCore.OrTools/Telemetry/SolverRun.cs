using System.Diagnostics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.OrTools.Telemetry;

/// <summary>
/// One solve measured by <see cref="SolverTelemetry"/>: a <c>crestapps.solver.solve</c> span, the
/// <c>crestapps.solver.duration</c> histogram and the <c>crestapps.solver.runs</c> counter, recorded when it is disposed.
/// </summary>
public sealed class SolverRun : IDisposable
{
    private readonly TelemetryScope _scope;
    private readonly string _solver;
    private readonly string _size;
    private string _status;
    private string _result;
    private bool _failed;
    private bool _disposed;

    internal SolverRun(string solver, string size)
    {
        _solver = solver;
        _size = size;
        _scope = TelemetryScope.Start(
            SolverTelemetry.ActivitySource,
            SolverTelemetry.SolveSpanName,
            SolverTelemetry.Duration,
            new TagList
            {
                { SolverTelemetry.SolverTag, solver },
                { SolverTelemetry.SizeTag, size },
            });
    }

    /// <summary>
    /// Gets how the search ended so far, or <see langword="null"/> until <see cref="Complete(string, bool)"/> is called.
    /// </summary>
    public string Status => _status;

    /// <summary>
    /// Records how the search ended and whose answer was returned.
    /// </summary>
    /// <param name="status">How the search ended, such as <see cref="SolverTelemetry.StatusOptimal"/>.</param>
    /// <param name="usedSolverAnswer"><see langword="true"/> when the solver's own answer was returned; <see langword="false"/>
    /// when the built-in solver's answer was kept.</param>
    public void Complete(string status, bool usedSolverAnswer)
    {
        ArgumentException.ThrowIfNullOrEmpty(status);

        _status = status;
        _result = usedSolverAnswer ? SolverTelemetry.ResultSolver : SolverTelemetry.ResultBuiltIn;
    }

    /// <summary>
    /// Records that the solve failed with an exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    public void Fail(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        _failed = true;
        _status ??= CrestAppsOrchardCoreTelemetry.GetOutcome(exception) == CrestAppsOrchardCoreTelemetry.OutcomeCancelled
            ? SolverTelemetry.StatusCancelled
            : CrestAppsOrchardCoreTelemetry.OutcomeError;
        _scope.Fail(exception);
    }

    /// <summary>
    /// Records the span, the duration and the run.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        var status = _status ?? CrestAppsOrchardCoreTelemetry.OutcomeError;
        var result = _result ?? SolverTelemetry.ResultBuiltIn;

        _scope.AddMetricTag(SolverTelemetry.StatusTag, status);
        _scope.AddMetricTag(CrestAppsOrchardCoreTelemetry.ResultTag, result);

        if (!_failed && _status is not null)
        {
            // A search the caller stopped still returns the built-in answer, but it did not run its course.
            _scope.SetOutcome(_status == SolverTelemetry.StatusCancelled
                ? CrestAppsOrchardCoreTelemetry.OutcomeCancelled
                : CrestAppsOrchardCoreTelemetry.OutcomeOk);
        }

        if (SolverTelemetry.Runs.Enabled)
        {
            var tags = new TagList
            {
                { SolverTelemetry.SolverTag, _solver },
                { SolverTelemetry.SizeTag, _size },
                { SolverTelemetry.StatusTag, status },
                { CrestAppsOrchardCoreTelemetry.ResultTag, result },
                { CrestAppsOrchardCoreTelemetry.OutcomeTag, _scope.Outcome },
            };

            CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
            SolverTelemetry.Runs.Add(1, tags);
        }

        _scope.Dispose();
    }
}
