using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Recipes;

/// <summary>
/// Makes the draft data pipeline that replaces the time clock's payroll CSV export, from columns a recipe gives or a
/// site saved, through Orchard's DataPipelines recipe step. An administrator publishes it under Tools > Data Pipelines.
/// </summary>
internal sealed class PayrollCsvPipelineImporter
{
    private const string DataPipelinesFeatureId = "OrchardCore.DataPipelines";

    private readonly IServiceProvider _services;
    private readonly ISession _session;
    private readonly ILogger _logger;

    public PayrollCsvPipelineImporter(IServiceProvider services, ISession session, ILogger<PayrollCsvPipelineImporter> logger)
    {
        _services = services;
        _session = session;
        _logger = logger;
    }

    /// <summary>
    /// Makes the draft pipeline from columns.
    /// </summary>
    /// <param name="columns">The columns, or none for the usual ones.</param>
    /// <param name="errors">Where the DataPipelines step reports what it couldn't do.</param>
    /// <returns>Whether the pipeline was made; without Data Pipelines turned on it is not.</returns>
    public async Task<bool> ImportAsync(IEnumerable<PayrollCsvColumn> columns, IList<string> errors)
    {
        if (_services.GetService<IShellFeaturesManager>() is not { } features ||
            !(await features.GetEnabledFeaturesAsync()).Any(feature => feature.Id == DataPipelinesFeatureId))
        {
            _logger.LogWarning("The payroll CSV columns were not turned into a data pipeline because Data Pipelines is not turned on. Turn it on and run the Payroll export pay inputs recipe.");

            return false;
        }

        var context = new RecipeExecutionContext
        {
            Name = "DataPipelines",
            Step = PayrollCsvPipeline.BuildStep(columns),
        };

        foreach (var handler in _services.GetServices<IRecipeStepHandler>())
        {
            await handler.ExecuteAsync(context);
        }

        foreach (var error in context.Errors)
        {
            errors?.Add(error);
        }

        return context.Errors.Count == 0;
    }

    /// <summary>
    /// Tries again, at a start of the site, to turn columns saved for the old export into the draft pipeline: quietly
    /// nothing until Data Pipelines is on, then once.
    /// </summary>
    /// <returns>A task.</returns>
    public async Task RetrySavedColumnsAsync()
    {
        if (_services.GetService<IShellFeaturesManager>() is not { } features
            || !(await features.GetEnabledFeaturesAsync()).Any(feature => feature.Id == DataPipelinesFeatureId)
            || await _session.Query<PayrollCsvSettings>(collection: WorkforceConstants.CollectionName).FirstOrDefaultAsync() is null)
        {
            return;
        }

        await ImportSavedColumnsAsync();
    }

    /// <summary>
    /// Turns the columns a site saved for the old export into the draft pipeline, once, then forgets them.
    /// </summary>
    /// <returns>A task.</returns>
    public async Task ImportSavedColumnsAsync()
    {
        var saved = await _session.Query<PayrollCsvSettings>(collection: WorkforceConstants.CollectionName).FirstOrDefaultAsync();

        if (saved is null)
        {
            return;
        }

        var errors = new List<string>();

        if (!await ImportAsync(saved.Columns, errors))
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                foreach (var error in errors)
                {
                    _logger.LogWarning("The saved payroll CSV columns could not be made a data pipeline: {Error}", error);
                }
            }

            return;
        }

        _session.Delete(saved, WorkforceConstants.CollectionName);
        await _session.SaveChangesAsync();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("The saved payroll CSV columns are now the draft data pipeline {PipelineId}.", PayrollCsvPipeline.PipelineId);
        }
    }
}
