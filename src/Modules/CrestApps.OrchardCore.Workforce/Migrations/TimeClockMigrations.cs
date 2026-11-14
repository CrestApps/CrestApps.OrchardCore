using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Recipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Migrations;

/// <summary>
/// Creates the time clock's tables and a Time Tracker stage for every activity code, and moves the pay periods and pay
/// codes it kept to Pay Setup.
/// </summary>
internal sealed class TimeClockMigrations : DataMigration
{
    private static readonly Dictionary<string, string[]> _paySetupGrants = new(StringComparer.Ordinal)
    {
        [WorkforcePermissions.ManageTimeClockPolicies.Name] = [PayrollPermissions.ManagePaySetup.Name],
    };

    // The export moved to Payroll's data and a data pipeline, and locking timesheets got its own permission.
    private static readonly Dictionary<string, string[]> _exportGrants = new(StringComparer.Ordinal)
    {
        ["ExportPayroll"] = [PayrollPermissions.ExportPayrollData.Name, WorkforcePermissions.LockTimesheets.Name],
        ["ViewPayRates"] = [PayrollPermissions.ExportPayrollData.Name],
    };

    private readonly TimeClockStageSeeder _stageSeeder;
    private readonly IStore _store;
    private readonly ISession _session;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    public TimeClockMigrations(
        TimeClockStageSeeder stageSeeder,
        IStore store,
        ISession session,
        ShellSettings shellSettings,
        ILogger<TimeClockMigrations> logger)
    {
        _stageSeeder = stageSeeder;
        _store = store;
        _session = session;
        _shellSettings = shellSettings;
        _logger = logger;
    }

    public async Task<int> CreateAsync()
    {
        // Pay periods and pay codes are Pay Setup's, which seeds the usual codes when it is turned on.
        await TimeClockIndexSchema.CreateAsync(SchemaBuilder);
        await _stageSeeder.SeedAsync();

        return 4;
    }

    // The time clock now keeps people's time in Time Tracker; the backfill task brings earlier punches in.
    public async Task<int> UpdateFrom1Async()
    {
        await _stageSeeder.SeedAsync();

        return 2;
    }

    // Pay periods and pay codes moved to Pay Setup with their ids; whoever managed them there keeps managing them.
    public async Task<int> UpdateFrom2Async()
    {
        var (schedules, codes) = await TimeClockPaySetupMove.MoveAsync(SchemaBuilder, _store, _session);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Moved {Schedules} pay periods and {Codes} pay codes to Pay Setup.", schedules, codes);
        }

        if (!_shellSettings.IsInitializing())
        {
            ShellScope.AddDeferredTask(scope => RolePermissionMigrations.GrantAsync(
                scope.ServiceProvider,
                _paySetupGrants,
                scope.ServiceProvider.GetRequiredService<ILogger<TimeClockMigrations>>()));
        }

        return 3;
    }

    // The payroll export is a data pipeline over the Pay Inputs data set now: roles keep what they could do, and the
    // columns a site chose become a draft pipeline to publish.
    public int UpdateFrom3()
    {
        if (!_shellSettings.IsInitializing())
        {
            ShellScope.AddDeferredTask(async scope =>
            {
                await RolePermissionMigrations.GrantAsync(
                    scope.ServiceProvider,
                    _exportGrants,
                    scope.ServiceProvider.GetRequiredService<ILogger<TimeClockMigrations>>());

                await scope.ServiceProvider.GetRequiredService<PayrollCsvPipelineImporter>().ImportSavedColumnsAsync();
            });
        }

        return 4;
    }
}
