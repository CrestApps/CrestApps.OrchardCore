using System.Globalization;
using CrestApps.OrchardCore.Payroll.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataPipelines.Models;
using OrchardCore.DataPipelines.Steps;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.DataPipelines;

/// <summary>
/// Saves rows as the inputs of pay runs, such as a file of bonuses read with Read a media file. Each row names the run
/// by its number (a <c>PayRun</c> field, or the run parameter <c>PayRun</c>), the person by <c>EmployeeNumber</c>, the
/// <c>PayCode</c>, and <c>Hours</c> or an <c>Amount</c> (<c>Rate</c>, <c>Date</c> and <c>Note</c> are optional). A
/// row that can't be saved goes to the Rejected output with the reason in an <c>Error</c> field. A run already
/// calculated is calculated again for the people whose inputs changed. The user the pipeline runs for must prepare the
/// runs of the run's employer.
/// </summary>
public sealed class SavePayInputsStep : DataPipelineStepType<SavePayInputsStep>, IDataPipelineStepType
{
    /// <summary>The field or run parameter that names the run by its number.</summary>
    public const string PayRunField = "PayRun";

    /// <summary>The field with the person's employee number.</summary>
    public const string EmployeeNumberField = "EmployeeNumber";

    /// <summary>The field with the pay code.</summary>
    public const string PayCodeField = "PayCode";

    /// <summary>The field with the hours.</summary>
    public const string HoursField = "Hours";

    /// <summary>The field with the amount.</summary>
    public const string AmountField = "Amount";

    /// <summary>The field with the rate.</summary>
    public const string RateField = "Rate";

    /// <summary>The field with the day of the work.</summary>
    public const string DateField = "Date";

    /// <summary>The field with a note.</summary>
    public const string NoteField = "Note";

    /// <summary>The field with what identifies a row in its file, so saving it again updates its input.</summary>
    public const string KeyField = "Key";

    /// <summary>The field the Rejected output adds with the reason.</summary>
    public const string ErrorField = "Error";

    /// <summary>The name of the Rejected output.</summary>
    public const string RejectedPort = "Rejected";

    /// <inheritdoc />
    public static string Name => PayrollConstants.SavePayInputsStepName;

    /// <inheritdoc />
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SavePayInputsStep>("Save pay inputs");

    /// <inheritdoc />
    public static LocalizationSource Description { get; } = LocalizationSource.Create<SavePayInputsStep>("Saves rows (EmployeeNumber, PayCode, Hours or Amount, and the run's number in PayRun) as the inputs of a pay run; rows that can't be saved go to Rejected with the reason.");

    /// <inheritdoc />
    public static DataPipelineStepCategory Category => DataPipelineStepCategory.Destination;

    /// <inheritdoc />
    public override IReadOnlyList<DataPipelinePort> GetInputs(DataPipelineStep step)
        => [new DataPipelinePort(DataPipelinePort.Input, "Rows", DataPipelinePortKind.Records) { IsRequired = true }];

    /// <inheritdoc />
    public override IReadOnlyList<DataPipelinePort> GetOutputs(DataPipelineStep step)
        => [new DataPipelinePort(RejectedPort, "Rejected", DataPipelinePortKind.Records)];

    /// <inheritdoc />
    public override Task DescribeAsync(DataPipelineDescribeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var fields = context.GetInputFields(DataPipelinePort.Input);

        if (fields.Count > 0)
        {
            if (fields.Find(EmployeeNumberField) is null || fields.Find(PayCodeField) is null)
            {
                context.AddError("The rows need the EmployeeNumber and PayCode fields: rename the file's columns with Select fields.");
            }

            if (fields.Find(HoursField) is null && fields.Find(AmountField) is null)
            {
                context.AddError("The rows need an Hours or an Amount field.");
            }

            if (fields.Find(PayRunField) is null)
            {
                context.AddWarning("The rows have no PayRun field: the run parameter PayRun must give the run's number.");
            }
        }

        context.SetOutputFields([.. fields, new DataField(ErrorField, "Error", DataFieldType.Text)], RejectedPort);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task ExecuteAsync(DataPipelineStepContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var importer = context.Services.GetRequiredService<PayRunInputImporter>();
        var rejected = context.GetOutput(RejectedPort);
        var origin = "Pipeline " + context.Run.PipelineName;
        var keyOrigin = "Pipeline " + context.Run.PipelineId;
        var parameter = context.Run.Parameters?.TryGetValue(PayRunField, out var value) == true ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;
        var saved = 0;
        var refused = 0;

        await foreach (var batch in context.GetInput(DataPipelinePort.Input).ReadBatchesAsync(context.CancellationToken))
        {
            var fields = batch.Fields.Append(new DataField(ErrorField, "Error", DataFieldType.Text)).ToList();
            var rows = new List<object[]>();

            string Text(object[] row, string name)
                => batch.Fields.IndexOf(name) is var index and >= 0 ? Convert.ToString(row[index], CultureInfo.InvariantCulture) : null;

            foreach (var row in batch.Rows)
            {
                var error = await importer.SaveAsync(new PayRunInputRow
                {
                    PayRun = Text(row, PayRunField) is { Length: > 0 } run ? run : parameter,
                    EmployeeNumber = Text(row, EmployeeNumberField),
                    PayCode = Text(row, PayCodeField),
                    Hours = Text(row, HoursField),
                    Amount = Text(row, AmountField),
                    Rate = Text(row, RateField),
                    Date = Text(row, DateField),
                    Note = Text(row, NoteField),

                    // Without a Key column a row is identified by its values, within this pipeline.
                    Key = Text(row, KeyField) is { Length: > 0 } key ? keyOrigin + ":" + key : keyOrigin,
                }, context.Run.User, origin, context.CancellationToken);

                if (error is null)
                {
                    saved++;
                }
                else
                {
                    refused++;
                    rows.Add([.. row, error]);
                }
            }

            if (rows.Count > 0 && rejected.IsConnected && !rejected.IsClosed)
            {
                await rejected.WriteAsync(new DataBatch(fields, rows), context.CancellationToken);
            }
        }

        await importer.RecalculateAsync(context.Run.User, context.CancellationToken);
        context.LogInformation($"{saved} pay inputs saved, {refused} rows rejected.");
    }
}
