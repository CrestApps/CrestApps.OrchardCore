using System.Security.Claims;
using CrestApps.OrchardCore.Core.SourceAccess;
using CrestApps.OrchardCore.Banking.Disbursements;
using CrestApps.OrchardCore.Banking.Disbursements.DataPipelines;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataPipelines.Models;
using OrchardCore.DataPipelines.Steps;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.Disbursements;

/// <summary>
/// The Read a disbursement batch file step hands the bank files of released batches to the pipeline, once each, and
/// only to a publisher who may manage disbursements.
/// </summary>
public sealed class ReadDisbursementBatchFileStepTests
{
    [Fact]
    public async Task ExecuteAsync_WritesTheFilesOfReleasedBatches_AndNextTimeOnlyNewOnes()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var ach = await context.AchAsync(origin, await context.PayeeAsync());
        var (batch, _) = await context.Service.CreateBatchAsync("Nacha", [ach.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);
        await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);
        var state = new DataPipelineState();
        var temporary = Directory.CreateTempSubdirectory("disbursement-step").FullName;

        try
        {
            // Act
            var first = await RunAsync(context, state, temporary, DisbursementsPermissions.ManageDisbursements);
            var second = await RunAsync(context, state, temporary, DisbursementsPermissions.ManageDisbursements);

            // Assert
            var file = Assert.Single(first);
            Assert.Equal(batch.FileName, file.FileName);
            Assert.Equal(context.Files.Files[batch.ItemId], await File.ReadAllBytesAsync(file.Path, TestContext.Current.CancellationToken));
            Assert.Empty(second);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ForAPublisherWhoMayNotManageDisbursements_Fails()
    {
        // Arrange
        var context = new DisbursementsTestContext();

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(context, new DataPipelineState(), Path.GetTempPath(), DisbursementsPermissions.ViewDisbursements));
    }

    private static async Task<List<DataPipelineFile>> RunAsync(DisbursementsTestContext context, DataPipelineState state, string temporary, params global::OrchardCore.Security.Permissions.Permission[] grants)
    {
        var services = new ServiceCollection()
            .AddSingleton(TestSecurity.Granting(grants))
            .AddSingleton<IDisbursementBatchManager>(context.Batches)
            .AddSingleton<IDisbursementFileStore>(context.Files)
            .AddSingleton<ISourceAccessService>(new SourceAccessService([]))
            .BuildServiceProvider();
        var output = new RecordingOutput();
        var step = new DataPipelineStep { StepId = "read", Type = ReadDisbursementBatchFileStep.StepName };
        var run = new DataPipelineRunContext
        {
            User = TestSecurity.User("admin"),
            State = state,
            TemporaryDirectory = temporary,
        };
        var stepContext = new DataPipelineStepContext(
            step,
            run,
            new Dictionary<string, DataPipelineInput>(),
            new Dictionary<string, DataPipelineOutput> { [DataPipelinePort.Output] = output },
            new DataPipelineStepMetrics(),
            _ => { },
            TestContext.Current.CancellationToken,
            services);

        await new ReadDisbursementBatchFileStep().ExecuteAsync(stepContext);

        return output.Files;
    }

    private sealed class RecordingOutput : DataPipelineOutput
    {
        public List<DataPipelineFile> Files { get; } = [];

        public override DataPipelinePort Port { get; } = new(DataPipelinePort.Output, "Files", DataPipelinePortKind.Files);

        public override bool IsConnected => true;

        public override bool IsClosed => false;

        public override IReadOnlyList<DataField> Fields => [];

        public override ValueTask WriteAsync(DataBatch batch, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        public override ValueTask WriteFileAsync(DataPipelineFile file, CancellationToken cancellationToken)
        {
            Files.Add(file);

            return ValueTask.CompletedTask;
        }
    }
}
