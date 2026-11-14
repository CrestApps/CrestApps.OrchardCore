using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataPipelines.Models;
using OrchardCore.DataPipelines.Steps;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.DataPipelines;

/// <summary>
/// Passes the rows of the Pay Inputs data set on unchanged and, once the whole run succeeds, records the records they
/// come from as exported, so the next run doesn't export them again. A run that fails, is cancelled or is a preview
/// records nothing, so the hours stay to export.
/// </summary>
public sealed class MarkPayInputsExportedStep : DataPipelineStepType<MarkPayInputsExportedStep>, IDataPipelineStepType
{
    /// <summary>
    /// The field that holds the kind of record a row comes from.
    /// </summary>
    public const string SourceKindField = "SourceKind";

    /// <summary>
    /// The field that holds the id of the record a row comes from.
    /// </summary>
    public const string SourceIdField = "SourceId";

    /// <summary>
    /// Gets the step type's name.
    /// </summary>
    public static string Name => PayrollConstants.MarkPayInputsExportedStepName;

    /// <summary>
    /// Gets the step type's name as shown.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<MarkPayInputsExportedStep>("Mark pay inputs exported");

    /// <summary>
    /// Gets what the step does.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<MarkPayInputsExportedStep>("Passes pay input rows on and, once the run succeeds, records them as exported so the next run leaves them out.");

    /// <summary>
    /// Gets the step type's category.
    /// </summary>
    public static DataPipelineStepCategory Category => DataPipelineStepCategory.Transform;

    /// <inheritdoc />
    public override Task DescribeAsync(DataPipelineDescribeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var fields = context.GetInputFields(DataPipelinePort.Input);

        if (fields.Count > 0 && (fields.Find(SourceKindField) is null || fields.Find(SourceIdField) is null))
        {
            context.AddError("The rows need the Source kind and Source ID fields of the Pay inputs data set.");
        }

        context.SetOutputFields(fields, DataPipelinePort.Output);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task ExecuteAsync(DataPipelineStepContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var input = context.GetInput(DataPipelinePort.Input);
        var output = context.GetOutput(DataPipelinePort.Output);
        var sources = new HashSet<PayInputReference>();

        await foreach (var batch in input.ReadBatchesAsync(context.CancellationToken))
        {
            Collect(batch, sources);

            if (output.IsConnected && !output.IsClosed)
            {
                await output.WriteAsync(batch, context.CancellationToken);
            }
        }

        if (context.Run.IsPreview || sources.Count == 0)
        {
            return;
        }

        // The publisher of the pipeline vouches for the export: they must be allowed to export payroll data.
        if (!await context.Services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(context.Run.User, PayrollPermissions.ExportPayrollData))
        {
            throw new InvalidOperationException("The user who published the pipeline may not export payroll data, so nothing is marked exported.");
        }

        var consumerId = PayrollConstants.PipelineConsumerPrefix + context.Run.RunId;
        var consumed = sources.ToArray();

        context.LogInformation($"{consumed.Length} records will be marked exported when the run succeeds.");

        context.Run.OnSucceeded((scope, cancellationToken) => scope.GetRequiredService<IPayInputService>().MarkConsumedAsync(new PayInputConsumption
        {
            ConsumerId = consumerId,
            Sources = consumed,
        }, cancellationToken));
    }

    /// <summary>
    /// Adds the records the rows of a batch come from.
    /// </summary>
    /// <param name="batch">The batch.</param>
    /// <param name="sources">The records found so far.</param>
    internal static void Collect(DataBatch batch, ISet<PayInputReference> sources)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(sources);

        var kind = batch.Fields.IndexOf(SourceKindField);
        var id = batch.Fields.IndexOf(SourceIdField);

        if (kind < 0 || id < 0)
        {
            return;
        }

        foreach (var row in batch.Rows)
        {
            if (row[kind] is string sourceKind && row[id] is string sourceId && !string.IsNullOrEmpty(sourceKind) && !string.IsNullOrEmpty(sourceId))
            {
                sources.Add(new PayInputReference(sourceKind, sourceId));
            }
        }
    }
}
