using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using CrestApps.OrchardCore.SourceAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataPipelines.Models;
using OrchardCore.DataPipelines.Steps;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Banking.Disbursements.DataPipelines;

/// <summary>
/// A data pipeline source of files: the bank files (Nacha) of the batches released since the last successful run, so
/// Orchard's SFTP, FTP, web API or e-mail steps deliver them to the bank. The run's user (who published the pipeline)
/// needs <see cref="DisbursementsPermissions.ManageDisbursements"/>, since a bank file holds account numbers, and must
/// be allowed to act on every disbursement of a batch for its file to be read.
/// </summary>
public sealed class ReadDisbursementBatchFileStep : DataPipelineStepType<ReadDisbursementBatchFileStep>, IDataPipelineStepType
{
    /// <summary>
    /// The step type's name.
    /// </summary>
    public const string StepName = "ReadDisbursementBatchFile";

    /// <summary>
    /// The state key of the release time of the last batch read.
    /// </summary>
    public const string WatermarkKey = "lastReleasedUtc";

    /// <inheritdoc />
    public static string Name => StepName;

    /// <inheritdoc />
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<ReadDisbursementBatchFileStep>("Read a disbursement batch file");

    /// <inheritdoc />
    public static LocalizationSource Description { get; } = LocalizationSource.Create<ReadDisbursementBatchFileStep>("Reads the bank files (ACH) of the disbursement batches released since the last successful run, to send to the bank.");

    /// <inheritdoc />
    public static DataPipelineStepCategory Category => DataPipelineStepCategory.Source;

    /// <inheritdoc />
    public override IReadOnlyList<DataPipelinePort> GetOutputs(DataPipelineStep step)
        => [new DataPipelinePort(DataPipelinePort.Output, "Files", DataPipelinePortKind.Files)];

    /// <inheritdoc />
    public override async Task ExecuteAsync(DataPipelineStepContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var output = context.GetOutput(DataPipelinePort.Output);
        var services = context.Services;

        if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(context.Run.User, DisbursementsPermissions.ManageDisbursements))
        {
            throw new InvalidOperationException("The user who published the pipeline may not manage disbursements, so bank files are not read.");
        }

        var batches = services.GetRequiredService<IDisbursementBatchManager>();
        var files = services.GetRequiredService<IDisbursementFileStore>();
        var access = await services.GetRequiredService<ISourceAccessService>().GetFilterAsync(context.Run.User, SourceAccessLevel.Manage, context.CancellationToken);
        var since = context.GetState<DateTime?>(WatermarkKey) ?? DateTime.MinValue;
        var last = since;
        var delivered = new List<string>();

        foreach (var batch in await batches.ListReleasedAsync(DisbursementsConstants.Providers.Nacha, since, context.CancellationToken))
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (output.IsClosed)
            {
                break;
            }

            if (!batch.HasFile || !Allows(access, batch))
            {
                context.LogWarning($"The batch {batch.ItemId} was skipped: it has no file, or the publisher may not act on all its disbursements.");

                continue;
            }

            var content = await files.ReadAsync(batch.ItemId, context.CancellationToken);

            if (content is null)
            {
                context.LogWarning($"The file of batch {batch.ItemId} can't be read.");

                continue;
            }

            var file = context.Run.CreateFile(batch.FileName ?? $"batch-{batch.ItemId}.txt", "text/plain");

            await File.WriteAllBytesAsync(file.Path, content, context.CancellationToken);
            await output.WriteFileAsync(file, context.CancellationToken);
            delivered.Add(batch.ItemId);

            if (batch.ReleasedUtc is { } released && released > last)
            {
                last = released;
            }
        }

        // Saved only when the run succeeds, so a failed delivery reads the same files again.
        if (!context.Run.IsPreview && last > since)
        {
            context.SetState(WatermarkKey, (DateTime?)last);
        }

        // Once the run succeeds, each batch shows when and by which pipeline its file went out, and it is recorded.
        if (!context.Run.IsPreview && delivered.Count > 0)
        {
            var pipeline = context.Run.PipelineName;

            context.Run.OnSucceeded(async (scope, cancellationToken) =>
            {
                var manager = scope.GetRequiredService<IDisbursementBatchManager>();
                var recorder = scope.GetRequiredService<DisbursementFileRecorder>();
                var now = scope.GetRequiredService<global::OrchardCore.Modules.IClock>().UtcNow;

                foreach (var id in delivered)
                {
                    if (await manager.FindByIdAsync(id, cancellationToken) is { } stamped)
                    {
                        stamped.DeliveredUtc = now;
                        stamped.DeliveredBy = pipeline;
                        await manager.UpdateAsync(stamped, cancellationToken: cancellationToken);
                        await recorder.RecordAsync(stamped, "delivered by a data pipeline", null, pipeline);
                    }
                }
            });
        }
    }

    private static bool Allows(SourceAccessFilter filter, DisbursementBatch batch)
        => Core.Services.DisbursementAccess.Allows(filter, batch);
}
