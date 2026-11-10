using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Migrations;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Documents;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks;

/// <summary>
/// Records the file imports made before leads recorded them on the leads they wrote, so the activity load's
/// <b>Imported file</b> filter lists them, a few thousand leads a minute until every lead has been read.
/// </summary>
/// <remarks>
/// This used to run once while the tenant activated. On a large tenant it outlived the command timeout and the step
/// was recorded anyway, so the filter never listed any file imported before it shipped. Run here, it holds up
/// nothing, and the cursor kept in <see cref="OmnichannelBackfillState"/> lets it resume after a restart.
/// </remarks>
[BackgroundTask(
    Title = "Omnichannel Lead Import Backfill",
    Schedule = "* * * * *",
    Description = "Records past file imports on the leads they wrote, so activity loads can filter by imported file.",
    LockTimeout = 5_000,
    LockExpiration = 300_000)]
public sealed class LeadImportBackfillBackgroundTask : IBackgroundTask
{
    internal const string BackfillName = "LeadImports";

    // Each page reads five hundred index rows, so a run reads at most ten thousand leads.
    private const int PagesPerRun = 20;

    private bool _completed;

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return this.RunAsync(run => BackfillAsync(serviceProvider, run, cancellationToken));
    }

    private async Task BackfillAsync(IServiceProvider serviceProvider, MeasuredTaskRun run, CancellationToken cancellationToken)
    {
        if (_completed)
        {
            run.Skip();

            return;
        }

        var stateManager = serviceProvider.GetRequiredService<IDocumentManager<OmnichannelBackfillState>>();
        var state = await stateManager.GetOrCreateMutableAsync();

        if (state.Completed.Contains(BackfillName))
        {
            _completed = true;
            run.Skip();

            return;
        }

        var definitions = await serviceProvider.GetRequiredService<IContentDefinitionManager>().ListTypeDefinitionsAsync();
        var leadTypes = definitions
            .Where(definition => OmnichannelRecordKinds.HasPart(definition, OmnichannelConstants.ContentParts.Lead))
            .Select(definition => definition.Name)
            .ToArray();

        var step = await LeadImportBackfill.RunStepAsync(
            serviceProvider.GetRequiredService<IStore>(),
            leadTypes,
            state.Cursors.GetValueOrDefault(BackfillName),
            PagesPerRun,
            serviceProvider.GetRequiredService<ILogger<LeadImportBackfillBackgroundTask>>(),
            cancellationToken);

        state.Cursors[BackfillName] = step.Cursor;

        run.AddItems(step.Recorded, "recorded");
        if (step.Completed)
        {
            state.Completed.Add(BackfillName);
            state.Cursors.Remove(BackfillName);
            _completed = true;
        }
        else
        {
            // The run read its pages and stopped; the leads after its cursor wait for the next run.
            run.BudgetExceeded();
        }

        await stateManager.UpdateAsync(state);
    }
}
