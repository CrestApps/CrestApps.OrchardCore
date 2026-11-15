using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Recipes;

/// <summary>
/// At each start, turns the payroll CSV columns a site saved for the old export into the draft pipeline once Data
/// Pipelines is on, so an upgrade with Data Pipelines off doesn't strand them.
/// </summary>
internal sealed class PayrollCsvColumnsRetry : ModularTenantEvents
{
    public override Task ActivatedAsync()
    {
        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<PayrollCsvPipelineImporter>().RetrySavedColumnsAsync());

        return Task.CompletedTask;
    }
}
