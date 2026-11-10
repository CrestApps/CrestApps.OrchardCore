using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.OrTools.Services;
using CrestApps.OrchardCore.OrTools.Telemetry;
using CrestApps.OrchardCore.Tests.Workforce;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Tests.Telemetry.Scheduling;

/// <summary>
/// The OR-Tools solvers record <c>crestapps.solver.runs</c> with the solver, how the search ended and the class of
/// the problem's size, and never a tag a metric must not carry. The names are a public contract dashboards depend on.
/// </summary>
public sealed class SolverTelemetryTests
{
    private static readonly string[] _searchStatuses =
    [
        SolverTelemetry.StatusOptimal,
        SolverTelemetry.StatusFeasible,
        SolverTelemetry.StatusTimedOut,
    ];

    [Fact]
    public async Task ShiftSolver_RecordsTheRun()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var problem = new ShiftScheduleProblem
        {
            Days = 1,
            Templates = [ShiftScheduleSolverTests.Template("day", 9 * 60, 8 * 60)],
            Workers = [ShiftScheduleSolverTests.Worker("a", days: [0]), ShiftScheduleSolverTests.Worker("b", days: [0])],
            Demands = ShiftScheduleSolverTests.Demand(problem: null, day: 0, from: 9 * 60, to: 17 * 60, minimum: 1, target: 1),
            TimeLimit = TimeSpan.FromSeconds(2),
        };

        // Act
        await new OrToolsShiftScheduleSolver().SolveAsync(problem, TestContext.Current.CancellationToken);

        // Assert
        // The other solver tests run in parallel and record runs too, so the assertions look for this run among them.
        var runs = capture.Find("crestapps.solver.runs", SolverTelemetry.SolverTag, SolverTelemetry.ShiftScheduleSolver);
        Assert.Contains(runs, run =>
            Equals(run.Tags[SolverTelemetry.SizeTag], "le_10") &&
            _searchStatuses.Contains(run.Tags[SolverTelemetry.StatusTag] as string) &&
            Equals(run.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag], CrestAppsOrchardCoreTelemetry.OutcomeOk));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public async Task ShiftSolver_WithNothingToSolve_RecordsASkippedRunThatKeptTheBuiltInAnswer()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var problem = new ShiftScheduleProblem
        {
            Days = 1,
            Templates = [ShiftScheduleSolverTests.Template("day", 9 * 60, 8 * 60)],
            Workers = [ShiftScheduleSolverTests.Worker("a", days: [0])],
        };

        // Act
        await new OrToolsShiftScheduleSolver().SolveAsync(problem, TestContext.Current.CancellationToken);

        // Assert
        var runs = capture.Find("crestapps.solver.runs", SolverTelemetry.SolverTag, SolverTelemetry.ShiftScheduleSolver);
        Assert.Contains(runs, run =>
            Equals(run.Tags[SolverTelemetry.StatusTag], SolverTelemetry.StatusSkipped) &&
            Equals(run.Tags[CrestAppsOrchardCoreTelemetry.ResultTag], SolverTelemetry.ResultBuiltIn));
    }

    [Fact]
    public async Task DispatchOptimizer_WithNoVisits_RecordsASkippedRun()
    {
        // Arrange
        using var capture = new TelemetryCapture();

        // Act
        await new OrToolsDispatchOptimizer().SolveAsync(new DispatchProblem(), TestContext.Current.CancellationToken);

        // Assert
        var runs = capture.Find("crestapps.solver.runs", SolverTelemetry.SolverTag, SolverTelemetry.DispatchSolver);
        Assert.Contains(runs, run =>
            Equals(run.Tags[SolverTelemetry.StatusTag], SolverTelemetry.StatusSkipped) &&
            Equals(run.Tags[SolverTelemetry.SizeTag], "le_10"));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Theory]
    [InlineData(0, "le_10")]
    [InlineData(10, "le_10")]
    [InlineData(11, "le_100")]
    [InlineData(1_000, "le_1000")]
    [InlineData(10_001, "le_100000")]
    [InlineData(100_001, "gt_100000")]
    public void GetSizeClass_GroupsSizesByPowersOfTen(long size, string expected)
    {
        // Act
        var sizeClass = SolverTelemetry.GetSizeClass(size);

        // Assert
        Assert.Equal(expected, sizeClass);
    }
}
