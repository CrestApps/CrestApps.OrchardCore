using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Core.Recipes;

/// <summary>
/// The OpenTelemetry instruments and log messages of the recipe steps of named catalogs,
/// under the meter named <c>CrestApps.OrchardCore.Recipes</c>.
/// </summary>
/// <remarks>
/// Orchard Core's recipe executor traces the recipe and its steps, so only what a step did with its entries is measured
/// here. The log messages use the event ids from <see cref="CrestAppsEventIds.ContentTransfer"/> + 710.
/// </remarks>
internal static partial class RecipeStepTelemetry
{
    /// <summary>
    /// The metric tag that records the name of the recipe step or deployment step, a name set in code.
    /// </summary>
    public const string StepTag = "step";

    /// <summary>
    /// The entry did not exist and was created.
    /// </summary>
    public const string ResultCreated = "created";

    /// <summary>
    /// The entry existed and was updated.
    /// </summary>
    public const string ResultUpdated = "updated";

    /// <summary>
    /// The entry failed validation and was not imported; the errors went to the recipe.
    /// </summary>
    public const string ResultInvalid = "invalid";

    /// <summary>
    /// The entry was left out before it was validated, such as an entry that cannot be imported or was merged into
    /// another one.
    /// </summary>
    public const string ResultSkipped = "skipped";

    /// <summary>
    /// Gets the meter of the recipe step instruments.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Recipes");

    /// <summary>
    /// Gets the number of catalog entries recipe steps read, tagged with the <see cref="StepTag"/> and the
    /// <see cref="CrestAppsOrchardCoreTelemetry.ResultTag"/>: <see cref="ResultCreated"/>, <see cref="ResultUpdated"/>,
    /// <see cref="ResultInvalid"/> or <see cref="ResultSkipped"/>.
    /// </summary>
    public static Counter<long> StepEntries { get; } = Meter.CreateCounter<long>(
        "crestapps.recipes.step.entries",
        "{entry}",
        "The number of catalog entries recipe steps read, by what became of them.");

    /// <summary>
    /// Records the entries of one recipe step that ended the same way.
    /// </summary>
    /// <param name="step">The name of the recipe step.</param>
    /// <param name="result">What became of the entries.</param>
    /// <param name="count">The number of entries. Zero and negative counts are ignored.</param>
    public static void RecordStepEntries(string step, string result, long count)
    {
        if (count <= 0 || !StepEntries.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { StepTag, step ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        StepEntries.Add(count, tags);
    }

    /// <summary>
    /// Logs that entries of a recipe step failed validation. Until now the errors only went to the recipe result.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="step">The name of the recipe step.</param>
    /// <param name="invalidCount">The number of entries that failed validation.</param>
    /// <param name="entryCount">The number of entries the step carries.</param>
    /// <param name="errorCount">The number of errors the recipe reports so far.</param>
    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 710,
        Level = LogLevel.Warning,
        Message = "The recipe step '{Step}' did not import {InvalidCount} of its {EntryCount} entries because they failed validation. The recipe reports {ErrorCount} errors.")]
    public static partial void EntriesFailedValidation(ILogger logger, string step, int invalidCount, int entryCount, int errorCount);

    /// <summary>
    /// Logs what a recipe step did with its entries.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="step">The name of the recipe step.</param>
    /// <param name="entryCount">The number of entries the step carries.</param>
    /// <param name="created">The number of entries created.</param>
    /// <param name="updated">The number of entries updated.</param>
    /// <param name="skipped">The number of entries skipped.</param>
    /// <param name="invalid">The number of entries that failed validation.</param>
    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 711,
        Level = LogLevel.Debug,
        Message = "The recipe step '{Step}' read {EntryCount} entries: {Created} created, {Updated} updated, {Skipped} skipped and {Invalid} invalid.")]
    public static partial void StepImported(ILogger logger, string step, int entryCount, int created, int updated, int skipped, int invalid);
}
