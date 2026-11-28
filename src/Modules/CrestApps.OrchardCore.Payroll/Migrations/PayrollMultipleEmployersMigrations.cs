using CrestApps.OrchardCore.Payroll.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundJobs;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Payroll.Migrations;

/// <summary>
/// Turns on Multiple Employers for a site that already pays the people of more than one employer, from before the
/// Payroll feature became the company's own payroll: its employers list, employer pickers and permissions per employer
/// stay where they were.
/// </summary>
internal sealed class PayrollMultipleEmployersMigrations : DataMigration
{
    private readonly ShellSettings _shellSettings;

    public PayrollMultipleEmployersMigrations(ShellSettings shellSettings)
    {
        _shellSettings = shellSettings;
    }

    public Task<int> CreateAsync()
    {
        // A new site has no employers yet.
        if (_shellSettings.IsInitializing())
        {
            return Task.FromResult(1);
        }

        // Migrations that run while a tenant starts run no deferred tasks, and the employer index may still be moving
        // to its table in this same start: the employers are counted after the request ends.
        return EnableWhenSeveralEmployersAsync();
    }

    private static async Task<int> EnableWhenSeveralEmployersAsync()
    {
        await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("payroll-enable-multiple-employers", static async scope =>
        {
            var features = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();

            if (await features.IsFeatureEnabledAsync(PayrollConstants.Features.MultipleEmployers)
                || (await scope.ServiceProvider.GetRequiredService<EmployerStore>().ListAsync()).Count < 2)
            {
                return;
            }

            var feature = (await features.GetAvailableFeaturesAsync()).FirstOrDefault(feature => feature.Id == PayrollConstants.Features.MultipleEmployers);

            if (feature is not null)
            {
                await features.EnableFeaturesAsync([feature], force: true);
            }
        });

        return 1;
    }
}
