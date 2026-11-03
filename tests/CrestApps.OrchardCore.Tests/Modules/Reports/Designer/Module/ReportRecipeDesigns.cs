using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

/// <summary>
/// Reads the report designs a module's recipe adds and runs them like the report runner does, so a recipe's reports are
/// checked against the real data sets they read.
/// </summary>
internal static class ReportRecipeDesigns
{
    /// <summary>
    /// Reads the designs of a recipe's <c>ReportDesigns</c> step, as the step reads them, and checks that the recipe turns
    /// on the Report Builder first.
    /// </summary>
    /// <param name="module">The folder of the module under <c>src/Modules</c>.</param>
    /// <param name="recipeFileName">The file name of the recipe in the module's <c>Recipes</c> folder.</param>
    /// <returns>The designs, in the order the recipe lists them.</returns>
    public static List<ReportDesign> Load(string module, string recipeFileName)
    {
        return ReportNodes(module, recipeFileName)
            .Select(node =>
            {
                var design = node.Deserialize<ReportDesign>(ReportDesignerJson.Options);

                design.Query = ReportDesignNormalizer.Normalize(design.Query);
                design.Visuals = ReportDesignNormalizer.Normalize(design.Visuals);

                return design;
            })
            .ToList();
    }

    /// <summary>
    /// Reads the group path each report of a recipe's <c>ReportDesigns</c> step names, such as <c>Store</c>.
    /// </summary>
    /// <param name="module">The folder of the module under <c>src/Modules</c>.</param>
    /// <param name="recipeFileName">The file name of the recipe in the module's <c>Recipes</c> folder.</param>
    /// <returns>The group paths, in the order the recipe lists the reports.</returns>
    public static List<string> GroupPaths(string module, string recipeFileName)
    {
        return ReportNodes(module, recipeFileName).Select(node => (string)node["group"]).ToList();
    }

    private static JsonArray ReportNodes(string module, string recipeFileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var recipe = JsonNode.Parse(File.ReadAllText(Path.Combine(directory.FullName, "src", "Modules", module, "Recipes", recipeFileName)));
        var steps = recipe["steps"].AsArray();

        Assert.False((bool)recipe["issetuprecipe"]);
        Assert.Equal("Feature", (string)steps[0]["name"]);
        Assert.Contains(ReportsConstants.BuilderFeature, steps[0]["enable"].AsArray().Select(feature => (string)feature));
        Assert.Equal("ReportDesigns", (string)steps[1]["name"]);

        return steps[1]["Reports"].AsArray();
    }

    /// <summary>
    /// Plans and runs each design over the data sources with the groupings its visuals need, and checks that it plans
    /// without errors and that its visuals fit its result.
    /// </summary>
    /// <param name="designs">The designs to run.</param>
    /// <param name="user">The user who runs the designs.</param>
    /// <param name="utcNow">The moment the designs run at.</param>
    /// <param name="dataSources">The data sources the designs read.</param>
    /// <returns>The result of each design, by its identifier.</returns>
    public static async Task<Dictionary<string, ReportQueryResult>> RunAsync(
        IEnumerable<ReportDesign> designs,
        ClaimsPrincipal user,
        DateTime utcNow,
        params TestDataSourceEntry[] dataSources)
    {
        var planner = ReportDesignerTestServices.Planner(dataSources);
        var engine = new ReportQueryEngine(planner, ReportDesignerTestServices.Formatter, new ReportQueryGate(), new PassThroughStringLocalizer<ReportQueryEngine>());
        var results = new Dictionary<string, ReportQueryResult>(StringComparer.Ordinal);

        foreach (var design in designs)
        {
            var context = ReportDesignerTestServices.Context(utcNow);
            context.DataSourceContext.User = user;

            var plan = await planner.PlanAsync(design.Query, context.DataSourceContext, TestContext.Current.CancellationToken);

            Assert.True(plan.IsValid, $"{design.ItemId}: {string.Join(" ", plan.Errors)}");

            foreach (var grouping in ReportDesignDocumentBuilder.GetGroupings(design.Visuals, ReportQueryEngine.DescribeColumns(plan)))
            {
                context.Groupings.Add(grouping);
            }

            var result = await engine.ExecuteAsync(plan, context, TestContext.Current.CancellationToken);

            Assert.Empty(ReportDesignerTestServices.DocumentBuilder().Validate(design.Visuals, result.Columns));
            results[design.ItemId] = result;
        }

        return results;
    }

    /// <summary>
    /// Reads the value of a column in a row of a result.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="row">The index of the row.</param>
    /// <param name="columnId">The identifier of the column.</param>
    /// <returns>The value.</returns>
    public static object Value(ReportQueryResult result, int row, string columnId)
    {
        var index = result.IndexOf(columnId);

        Assert.True(index >= 0, $"The result has no column '{columnId}'.");

        return result.Rows[row][index];
    }
}
