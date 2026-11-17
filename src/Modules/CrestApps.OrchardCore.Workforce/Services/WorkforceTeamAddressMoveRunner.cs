using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Records the data moves of Workforce that finished, so one that failed or was cut short runs again.
/// </summary>
public sealed class WorkforceDataMoves
{
    /// <summary>
    /// The move of a team's one-line address to its own structured address.
    /// </summary>
    public const string TeamAddress = "TeamAddress";

    /// <summary>
    /// Gets or sets the moves that finished.
    /// </summary>
    public HashSet<string> Completed { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Moves the one-line address Workforce kept on a team to the team's own address until the move has finished once.
/// The move is safe to run again: a team already moved has no old line left. It is marked done only when every team
/// was saved, and a failure is logged and retried at the next start of the tenant.
/// </summary>
internal sealed class WorkforceTeamAddressMoveRunner
{
    private readonly ISession _session;
    private readonly ITeamManager _teamManager;
    private readonly ILogger _logger;

    public WorkforceTeamAddressMoveRunner(ISession session, ITeamManager teamManager, ILogger<WorkforceTeamAddressMoveRunner> logger)
    {
        _session = session;
        _teamManager = teamManager;
        _logger = logger;
    }

    /// <summary>
    /// Runs the move unless it finished before.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether the move is done.</returns>
    public async Task<bool> RunAsync(CancellationToken cancellationToken = default)
    {
        var moves = await _session.Query<WorkforceDataMoves>(collection: WorkforceConstants.CollectionName).FirstOrDefaultAsync(cancellationToken)
            ?? new WorkforceDataMoves();

        if (moves.Completed.Contains(WorkforceDataMoves.TeamAddress))
        {
            return true;
        }

        try
        {
            var moved = await WorkforceTeamAddressMove.MoveAsync(_teamManager, cancellationToken);

            moves.Completed.Add(WorkforceDataMoves.TeamAddress);
            await _session.SaveAsync(moves, false, WorkforceConstants.CollectionName, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            if (moved > 0 && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Moved the address of {Count} teams to the team's own address.", moved);
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "The move of team addresses to the team's own address failed. It runs again at the next start of the tenant; no address is lost meanwhile.");

            return false;
        }
    }
}

/// <summary>
/// Runs the team address move once the tenant has started, until it has finished.
/// </summary>
internal sealed class WorkforceTeamAddressMoveTenantEvents : ModularTenantEvents
{
    private readonly ShellSettings _shellSettings;

    public WorkforceTeamAddressMoveTenantEvents(ShellSettings shellSettings)
    {
        _shellSettings = shellSettings;
    }

    public override Task ActivatedAsync()
    {
        if (_shellSettings.IsInitializing())
        {
            return Task.CompletedTask;
        }

        // After the start's own work, migrations included, in a scope of its own.
        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<WorkforceTeamAddressMoveRunner>().RunAsync());

        return Task.CompletedTask;
    }
}
