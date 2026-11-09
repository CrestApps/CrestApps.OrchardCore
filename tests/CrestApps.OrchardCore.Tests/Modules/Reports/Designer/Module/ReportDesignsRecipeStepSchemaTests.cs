using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Recipes;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using CrestApps.OrchardCore.Reports.Models;
using Json.Schema;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

public sealed class ReportDesignsRecipeStepSchemaTests
{
    [Fact]
    public async Task Schema_AcceptsWhatTheDeploymentStepExports()
    {
        // Arrange: the step written the way the deployment source writes it.
        var step = new JsonObject
        {
            ["name"] = ReportDesignsRecipeStep.Name,
            ["Groups"] = new JsonArray(ToNode(new ReportGroup { ItemId = "sales", DisplayText = "Sales", CreatedUtc = DateTime.UtcNow })),
            ["Views"] = new JsonArray(ToNode(new ReportView
            {
                ItemId = "agents-view",
                DisplayText = "Agents",
                RefreshIntervalMinutes = 60,
                Query = new ReportQueryDefinition
                {
                    DataSets = [new ReportDataSetReference { Alias = "u", Source = "Users" }],
                    Columns = [new ReportColumnDefinition { Id = "c1", Field = "u.UserName" }],
                },
            })),
            ["Reports"] = new JsonArray(ToNode(Design())),
        };

        step["Reports"][0]["OwnerUserName"] = "admin";

        // Act
        var schema = await new ReportDesignsRecipeStepSchema().GetSchemaAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(Evaluate(schema, step));
    }

    [Fact]
    public async Task Schema_AcceptsTheReportDesignsStepsTheModulesShip()
    {
        // Arrange
        var schema = await new ReportDesignsRecipeStepSchema().GetSchemaAsync(TestContext.Current.CancellationToken);
        var steps = ShippedSteps().ToArray();

        // Act
        var refused = steps.Where(step => !Evaluate(schema, step.Step)).Select(step => step.File).ToArray();

        // Assert
        Assert.NotEmpty(steps);
        Assert.Empty(refused);
    }

    [Fact]
    public async Task Schema_RefusesAReportWithoutAValidItemId()
    {
        // Arrange
        var step = new JsonObject
        {
            ["name"] = ReportDesignsRecipeStep.Name,
            ["Reports"] = new JsonArray(new JsonObject { ["itemId"] = "not valid!", ["displayText"] = "Report" }),
        };

        // Act
        var schema = await new ReportDesignsRecipeStepSchema().GetSchemaAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Evaluate(schema, step));
    }

    private static ReportDesign Design()
        => new()
        {
            ItemId = "by-agent",
            DisplayText = "Activities by agent",
            GroupId = "sales",
            Query = new ReportQueryDefinition
            {
                DataSets = [new ReportDataSetReference { Alias = "a", Source = "ReportViews", DataSet = "agents-view" }],
                CalculatedFields = [new ReportCalculatedField { Name = "Upper", Expression = "UPPER([a.c1])" }],
                Filters = [new ReportFilterDefinition { Id = "f1", Field = "a.c1", Exposed = true, Values = ["x"] }],
                Columns =
                [
                    new ReportColumnDefinition { Id = "k1", Field = "a.c1" },
                    new ReportColumnDefinition { Id = "k2", Field = "a.c1", Aggregate = DataAggregate.Count, Colors = new ReportColumnColors { Color = "#000" } },
                ],
                Sorts = [new ReportSortDefinition { ColumnId = "k2", Descending = true }],
                Limit = 50,
            },
            Queries = [new ReportNamedQuery { Id = "q2", Name = "Second" }],
            Containers = [new ReportContainerDefinition { Id = "c", Title = "Box", Height = 300 }],
            Visuals =
            [
                new ReportVisualDefinition { Id = "t", Type = ReportVisualType.Table, ContainerId = "c", ShowTotals = true },
                new ReportVisualDefinition
                {
                    Id = "chart",
                    Type = ReportVisualType.Chart,
                    ChartType = ReportChartType.Bar,
                    CategoryColumnId = "k1",
                    ValueColumnIds = ["k2"],
                    Action = new ReportVisualAction { ColumnId = "k1", Targets = [new ReportActionTarget { VisualId = "t" }] },
                    ReferenceLines = [new ReportReferenceLine { ColumnId = "k2", Kind = ReportReferenceLineKind.Average }],
                },
            ],
            SharedRoles = ["Editor"],
            CreatedUtc = DateTime.UtcNow,
        };

    private static JsonNode ToNode<T>(T item)
        => JsonSerializer.SerializeToNode(item, ReportDesignerJson.Options);

    private static IEnumerable<(string File, JsonObject Step)> ShippedSteps()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        foreach (var file in Directory.EnumerateFiles(Path.Combine(directory.FullName, "src", "Modules"), "*.recipe.json", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);

            if (!text.Contains($"\"{ReportDesignsRecipeStep.Name}\"", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var step in JsonNode.Parse(text)["steps"].AsArray().OfType<JsonObject>())
            {
                if (step["name"]?.GetValue<string>() == ReportDesignsRecipeStep.Name)
                {
                    yield return (Path.GetFileName(file), step);
                }
            }
        }
    }

    private static bool Evaluate(JsonSchema schema, JsonObject step)
    {
        using var document = JsonDocument.Parse(step.ToJsonString());

        return schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Flag }).IsValid;
    }
}
