using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.DataPipelines;
using CrestApps.OrchardCore.Payroll.DataSources;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// The pay inputs seam: the service that reads every source and tells each what was taken, the Pay Inputs data set,
/// the Mark pay inputs exported step, and the pipelines that replace the time clock's payroll CSV.
/// </summary>
public sealed class PayInputsTests
{
    [Fact]
    public async Task Service_ReadsEverySource_AndTellsEachOnlyOfItsOwnRecords()
    {
        var timesheets = new FakeSource("Timesheet", new PayInput { SourceId = "t1", Hours = 8 });
        var tips = new FakeSource("Tips", new PayInput { SourceId = "p1", Amount = 20 });
        var service = new DefaultPayInputService([timesheets, tips]);

        var inputs = await service.GetInputsAsync(new PayInputQuery(), TestContext.Current.CancellationToken);

        await service.MarkConsumedAsync(new PayInputConsumption
        {
            ConsumerId = "run-1",
            Sources = [new("Timesheet", "t1"), new("Timesheet", "t1"), new("Tips", "p1"), new("Gone", "x")],
        }, TestContext.Current.CancellationToken);

        await service.ReleaseAsync(new PayInputConsumption { ConsumerId = "run-1", Sources = [new("Tips", "p1")] }, TestContext.Current.CancellationToken);

        Assert.Equal([("Timesheet", "t1"), ("Tips", "p1")], inputs.Select(input => (input.SourceKind, input.SourceId)));
        Assert.Equal([("run-1", "t1")], timesheets.Marked);
        Assert.Equal([("run-1", "p1")], tips.Marked);
        Assert.Equal([("run-1", "p1")], tips.Released);
        Assert.Empty(timesheets.Released);
    }

    [Fact]
    public async Task Service_RefusesAConsumptionWithoutAConsumer()
    {
        var service = new DefaultPayInputService([]);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.MarkConsumedAsync(new PayInputConsumption { Sources = [new("Timesheet", "t1")] }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Amount_IsTheInputsOwn_OrThePremium_OrTheRateTimesTheMultiplier_AndNothingForUnpaidTime()
    {
        var overtime = new PayCode { Kind = PayCodeKinds.Overtime, Multiplier = 1.5m };
        var night = new PayCode { Kind = PayCodeKinds.ShiftDifferential, Multiplier = 1, PremiumPerHour = 2.25m };
        var unpaid = new PayCode { Kind = PayCodeKinds.Unpaid };

        Assert.Equal(45m, PayInputsDataSet.AmountOf(new PayInput { Hours = 2, Rate = 15 }, overtime));
        Assert.Equal(4.5m, PayInputsDataSet.AmountOf(new PayInput { Hours = 2, Rate = 15 }, night));
        Assert.Equal(0m, PayInputsDataSet.AmountOf(new PayInput { Hours = 2, Rate = 15 }, unpaid));
        Assert.Null(PayInputsDataSet.AmountOf(new PayInput { Hours = 2 }, overtime));
        Assert.Equal(100m, PayInputsDataSet.AmountOf(new PayInput { Amount = 100 }, null));

        // Not rounded: the pipeline sums, then rounds.
        Assert.Equal(16.6665m, PayInputsDataSet.AmountOf(new PayInput { Hours = 1.1111m, Rate = 15 }, new PayCode { Multiplier = 1 }));
    }

    [Fact]
    public async Task DataSet_IsRead_OnlyByThoseWhoMayExportPayrollData()
    {
        var allowed = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "payroll")], "test"));
        var refused = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "clerk")], "test"));
        var authorization = new Mock<IAuthorizationService>();

        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                user == allowed && requirements.OfType<PermissionRequirement>().All(requirement => requirement.Permission.Name == PayrollPermissions.ExportPayrollData.Name)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        var dataSet = CreateDataSet(authorization.Object);

        Assert.True(await dataSet.CanReadAsync(new DataSourceContext { User = allowed }, TestContext.Current.CancellationToken));
        Assert.False(await dataSet.CanReadAsync(new DataSourceContext { User = refused }, TestContext.Current.CancellationToken));
        Assert.False(await dataSet.CanReadAsync(new DataSourceContext(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void MarkStep_CollectsEachRecordOnce_AndSkipsRowsWithout()
    {
        DataField[] fields = [new("PayCode", "PayCode", DataFieldType.Text), new("SourceKind", "SourceKind", DataFieldType.Text), new("SourceId", "SourceId", DataFieldType.Text)];
        var sources = new HashSet<PayInputReference>();

        MarkPayInputsExportedStep.Collect(new DataBatch(fields, [["REG", "Timesheet", "t1"], ["OT", "Timesheet", "t1"], ["REG", "Timesheet", "t2"], ["REG", null, "t3"]]), sources);
        MarkPayInputsExportedStep.Collect(new DataBatch([new DataField("PayCode", "PayCode", DataFieldType.Text)], [["REG"]]), sources);

        Assert.Equal([new PayInputReference("Timesheet", "t1"), new PayInputReference("Timesheet", "t2")], sources.OrderBy(source => source.SourceId));
    }

    [Fact]
    public void ShippedRecipe_ReadsOnlyFieldsThePayInputsDataSetHas_AndMarksBeforeTheFile()
    {
        var path = Path.Combine(FindRepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.Payroll", "Recipes", "payroll-pay-inputs-export.recipe.json");
        var recipe = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var pipeline = recipe["steps"]![1]!["Pipelines"]![0]!.AsObject();

        AssertPipelineReadsPayInputs(pipeline);
    }

    [Fact]
    public void ColumnsPipeline_UsesTheColumnsInOrder_UnderUniqueHeadings_AndKeepsARowPerTimesheetAndCode()
    {
        var pipeline = PayrollCsvPipeline.Build(
        [
            new PayrollCsvColumn { Header = "Badge", Field = PayrollCsvField.EmployeeNumber },
            new PayrollCsvColumn { Header = "Day", Field = PayrollCsvField.Date },
            new PayrollCsvColumn { Header = "Hours", Field = PayrollCsvField.Hours },
            new PayrollCsvColumn { Header = "Hours", Field = PayrollCsvField.Amount },
        ]);
        var steps = pipeline["Definition"]!["Steps"]!.AsArray().Select(step => step!.AsObject()).ToDictionary(step => step["StepId"]!.GetValue<string>());
        var groupBy = steps["group"]["Properties"]!["AggregateStepSettings"]!["GroupBy"]!.AsArray().Select(node => node!.GetValue<string>()).ToList();
        var columns = steps["columns"]["Properties"]!["SelectFieldsStepSettings"]!["Fields"]!.AsArray().Select(node => (node!["Field"]!.GetValue<string>(), node["Name"]!.GetValue<string>())).ToList();
        var sort = steps["sort"]["Properties"]!["SortStepSettings"]!["Keys"]!.AsArray().Select(node => node!["Field"]!.GetValue<string>()).ToList();

        AssertPipelineReadsPayInputs(pipeline);
        Assert.Equal(PayrollCsvPipeline.PipelineId, pipeline["PipelineId"]!.GetValue<string>());
        Assert.Equal(["StaffMemberId", "StaffMemberName", "PeriodStart", "PayCodeId", "PayCode", "EmployeeNumber", "Date"], groupBy);
        Assert.Equal([("EmployeeNumber", "Badge"), ("Date", "Day"), ("Hours", "Hours"), ("Amount", "Hours2")], columns);
        Assert.Equal(["StaffMemberName", "StaffMemberId", "PeriodStart", "Date", "PayCode"], sort);
    }

    [Fact]
    public void ColumnsPipeline_WithoutColumns_UsesTheUsualOnes()
    {
        var pipeline = PayrollCsvPipeline.Build([]);
        var columns = pipeline["Definition"]!["Steps"]!.AsArray().Single(step => step!["StepId"]!.GetValue<string>() == "columns")!["Properties"]!["SelectFieldsStepSettings"]!["Fields"]!.AsArray()
            .Select(node => node!["Name"]!.GetValue<string>());

        Assert.Equal(["EmployeeId", "Name", "PeriodStart", "PeriodEnd", "PayCode", "Hours", "Rate", "Amount"], columns);
    }

    private static void AssertPipelineReadsPayInputs(JsonObject pipeline)
    {
        var fields = CreateDataSet(Mock.Of<IAuthorizationService>()).Fields.Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
        var steps = pipeline["Definition"]!["Steps"]!.AsArray().Select(step => step!.AsObject()).ToList();
        var source = steps.Single(step => step["Type"]!.GetValue<string>() == "DataSource")["Properties"]!["DataSourceStepSettings"]!;
        var read = source["Fields"]!.AsArray().Select(node => node!.GetValue<string>()).ToList();
        var order = pipeline["Definition"]!["Connections"]!.AsArray().Select(connection => connection!["TargetStepId"]!.GetValue<string>()).ToList();
        var group = steps.Single(step => step["Type"]!.GetValue<string>() == "Aggregate")["Properties"]!["AggregateStepSettings"]!["GroupBy"]!.AsArray().Select(node => node!.GetValue<string>());

        Assert.Equal(PayrollConstants.DataSourceName, source["Source"]!.GetValue<string>());
        Assert.Equal(PayrollConstants.PayInputsDataSetName, source["DataSet"]!.GetValue<string>());
        Assert.All(read, field => Assert.Contains(field, fields));
        Assert.Contains("SourceKind", read);
        Assert.Contains("SourceId", read);
        Assert.All(group, field => Assert.Contains(field, read));

        // The step that marks the inputs exported comes before the file, so a run that fails to write or deliver it
        // never marks anything.
        Assert.True(order.IndexOf("mark") < order.IndexOf("file"));
        Assert.Equal(PayrollConstants.MarkPayInputsExportedStepName, steps.Single(step => step["StepId"]!.GetValue<string>() == "mark")["Type"]!.GetValue<string>());
    }

    private static PayInputsDataSet CreateDataSet(IAuthorizationService authorization)
        => new(Mock.Of<IPayInputService>(), Mock.Of<IPayCodeManager>(), Mock.Of<IPayScheduleManager>(), authorization, RecipeRoundTrip.Localizer<PayInputsDataSet>());

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }

    private sealed class FakeSource : IPayInputSource
    {
        private readonly PayInput[] _inputs;

        public FakeSource(string kind, params PayInput[] inputs)
        {
            SourceKind = kind;
            _inputs = inputs;
        }

        public string SourceKind { get; }

        public List<(string ConsumerId, string SourceId)> Marked { get; } = [];

        public List<(string ConsumerId, string SourceId)> Released { get; } = [];

        public Task<IReadOnlyList<PayInput>> GetInputsAsync(PayInputQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PayInput>>(_inputs);

        public Task MarkConsumedAsync(string consumerId, IReadOnlyCollection<string> sourceIds, CancellationToken cancellationToken = default)
        {
            Marked.AddRange(sourceIds.Select(id => (consumerId, id)));

            return Task.CompletedTask;
        }

        public Task ReleaseAsync(string consumerId, IReadOnlyCollection<string> sourceIds, CancellationToken cancellationToken = default)
        {
            Released.AddRange(sourceIds.Select(id => (consumerId, id)));

            return Task.CompletedTask;
        }
    }
}
