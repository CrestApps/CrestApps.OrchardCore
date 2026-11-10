using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Core.Recipes;

/// <summary>
/// Imports the entries of a named catalog that a recipe step carries in one array, creating the entries that do not
/// exist and updating those that do.
/// </summary>
/// <remarks>
/// An entry is matched by its identifier, and by its name too when <see cref="MatchByName"/> says so. An update is
/// checked on a detached copy first, so an invalid entry is reported as a recipe error instead of being written to
/// the store. The step reads the same JSON shape the matching
/// <see cref="Deployments.NamedCatalogDeploymentSource{TStep, TEntry}"/> writes.
/// <para>
/// Every step is measured: a <c>crestapps.recipes.step</c> span, and its entries counted on
/// <c>crestapps.recipes.step.entries</c> by what became of them (created, updated, invalid or skipped). Entries that
/// fail validation are also logged as a warning, since their errors otherwise only reach the recipe result.
/// </para>
/// </remarks>
/// <typeparam name="TEntry">The catalog entry type.</typeparam>
public abstract class NamedCatalogRecipeStep<TEntry> : CatalogRecipeStep<TEntry>
    where TEntry : CatalogItem, INameAwareModel, new()
{
    private readonly INamedCatalogManager<TEntry> _manager;
    private readonly string _stepName;
    private readonly string _entriesName;
    private readonly AsyncLocal<StepTally> _tally = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedCatalogRecipeStep{TEntry}"/> class.
    /// </summary>
    /// <param name="stepName">The name of the recipe step.</param>
    /// <param name="entriesName">The name of the step member that holds the array of entries.</param>
    /// <param name="manager">The manager the entries are created and updated through.</param>
    protected NamedCatalogRecipeStep(
        string stepName,
        string entriesName,
        INamedCatalogManager<TEntry> manager)
        : base(stepName, entriesName, manager)
    {
        _manager = manager;
        _stepName = stepName;
        _entriesName = entriesName;
    }

    /// <summary>
    /// Gets whether an entry without a known identifier is matched to a stored entry of the same name.
    /// </summary>
    protected virtual bool MatchByName => false;

    /// <inheritdoc/>
    protected override async ValueTask<TEntry> FindExistingAsync(string id, JsonObject data)
    {
        var entry = await base.FindExistingAsync(id, data);

        if (entry is null && MatchByName)
        {
            var name = data[nameof(INameAwareModel.Name)] is JsonValue value && value.TryGetValue<string>(out var text) ? text?.Trim() : null;

            entry = !string.IsNullOrEmpty(name) ? await _manager.FindByNameAsync(name) : null;
        }

        return entry;
    }

    /// <inheritdoc/>
    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The tally flows with the call into the validation and creation hooks below, and only into them.
        var tally = new StepTally();
        _tally.Value = tally;

        using var telemetry = TelemetryScope.Start(RecipeStepTelemetry.ActivitySource, RecipeStepTelemetry.StepSpanName, duration: null);
        telemetry.SetSpanTag(RecipeStepTelemetry.StepSpanTag, _stepName);

        try
        {
            await base.HandleAsync(context);
            telemetry.Succeed();
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);

            throw;
        }
        finally
        {
            _tally.Value = null;
            Record(tally, context, telemetry);
        }
    }

    /// <inheritdoc/>
    protected override async ValueTask<ValidationResultDetails> ValidateAsync(TEntry entry)
    {
        var result = await base.ValidateAsync(entry);

        if (_tally.Value is { } tally)
        {
            if (result.Succeeded)
            {
                tally.Valid++;
            }
            else
            {
                tally.Invalid++;
            }
        }

        return result;
    }

    /// <inheritdoc/>
    protected override async ValueTask CreateAsync(TEntry entry)
    {
        await base.CreateAsync(entry);

        if (_tally.Value is { } tally)
        {
            tally.Created++;
        }
    }

    private void Record(StepTally tally, RecipeExecutionContext context, TelemetryScope telemetry)
    {
        var entryCount = CatalogRecipeSections.Section(context.Step, _entriesName).Count();

        // An entry that passed validation and was not created was an update. Entries that never reached validation
        // were left out; a step that failed part way did not reach the rest, so they are not counted as skipped.
        var updated = Math.Max(0, tally.Valid - tally.Created);
        var skipped = telemetry.Outcome == CrestAppsOrchardCoreTelemetry.OutcomeOk
            ? Math.Max(0, entryCount - tally.Valid - tally.Invalid)
            : 0;

        RecipeStepTelemetry.RecordStepEntries(_stepName, RecipeStepTelemetry.ResultCreated, tally.Created);
        RecipeStepTelemetry.RecordStepEntries(_stepName, RecipeStepTelemetry.ResultUpdated, updated);
        RecipeStepTelemetry.RecordStepEntries(_stepName, RecipeStepTelemetry.ResultInvalid, tally.Invalid);
        RecipeStepTelemetry.RecordStepEntries(_stepName, RecipeStepTelemetry.ResultSkipped, skipped);

        telemetry.SetSpanTag("crestapps.recipes.entries", entryCount);
        telemetry.SetSpanTag("crestapps.recipes.entries.invalid", tally.Invalid);

        var logger = ShellScope.Services?.GetService<ILoggerFactory>()?.CreateLogger(GetType());

        if (logger is null)
        {
            return;
        }

        if (tally.Invalid > 0)
        {
            RecipeStepTelemetry.EntriesFailedValidation(logger, _stepName, tally.Invalid, entryCount, context.Errors?.Count ?? 0);
        }

        RecipeStepTelemetry.StepImported(logger, _stepName, entryCount, tally.Created, updated, skipped, tally.Invalid);
    }

    private sealed class StepTally
    {
        public int Created { get; set; }

        public int Valid { get; set; }

        public int Invalid { get; set; }
    }
}
