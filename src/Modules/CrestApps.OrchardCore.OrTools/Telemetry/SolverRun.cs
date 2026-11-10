using System.Diagnostics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.OrTools.Telemetry;

/// <summary>
/// One solve measured by <see cref="SolverTelemetry"/>: the <c>crestapps.solver.runs</c> counter, recorded when it is
/// disposed. The solve's duration is not measured: a guided local search runs to its time limit, so it would only
/// repeat the limit.
/// </summary>
public sealed class SolverRun : IDisposable
{
    private readonly string _solver;
    private readonly string _size;
    private string _status;
    private string _result;
    private string _failedOutcome;
    private bool _disposed;

    internal SolverRun(string solver, string size)
    {
        _solver = solver;
        _size = size;
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

        _failedOutcome = CrestAppsOrchardCoreTelemetry.GetOutcome(exception);
        _status ??= _failedOutcome == CrestAppsOrchardCoreTelemetry.OutcomeCancelled
            ? SolverTelemetry.StatusCancelled
            : CrestAppsOrchardCoreTelemetry.OutcomeError;
    }

    /// <summary>
    /// Records the run.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (!SolverTelemetry.Runs.Enabled)
        {
            return;
        }

        var status = _status ?? CrestAppsOrchardCoreTelemetry.OutcomeError;

        // A search the caller stopped still returns the built-in answer, but it did not run its course.
        var outcome = _failedOutcome
            ?? (_status is null
                ? CrestAppsOrchardCoreTelemetry.OutcomeError
                : _status == SolverTelemetry.StatusCancelled
                    ? CrestAppsOrchardCoreTelemetry.OutcomeCancelled
                    : CrestAppsOrchardCoreTelemetry.OutcomeOk);

        var tags = new TagList
        {
            { SolverTelemetry.SolverTag, _solver },
            { SolverTelemetry.SizeTag, _size },
            { SolverTelemetry.StatusTag, status },
            { CrestAppsOrchardCoreTelemetry.ResultTag, _result ?? SolverTelemetry.ResultBuiltIn },
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        SolverTelemetry.Runs.Add(1, tags);
    }
}
