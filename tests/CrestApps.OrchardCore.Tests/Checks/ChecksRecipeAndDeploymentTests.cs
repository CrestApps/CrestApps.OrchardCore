using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking.Checks.Core.Services;
using CrestApps.OrchardCore.Banking.Checks.Deployments;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Recipes;
using CrestApps.OrchardCore.Banking.Checks.Reports;
using CrestApps.OrchardCore.Core.SourceAccess;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Authorization;
using Moq;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Checks;

public sealed class ChecksRecipeAndDeploymentTests
{
    [Fact]
    public async Task CheckLayoutsDeployment_LeavesOutWhetherFilesAreStored()
    {
        // Arrange
        var manager = new Mock<INamedCatalogManager<CheckLayout>>();
        manager.Setup(entry => entry.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new CheckLayout { ItemId = "l1", Name = "Blank", HasSignature = true, HasMicrFont = true, PrintMicr = true },
        ]);

        // Act
        var step = await RecipeRoundTrip.ExportAsync(new CheckLayoutsDeploymentSource(manager.Object), new CheckLayoutsDeploymentStep());

        // Assert
        var entry = Assert.Single(step[CheckLayoutsRecipeStep.EntriesName]!.AsArray())!.AsObject();
        Assert.Equal("Blank", entry["Name"]!.GetValue<string>());
        Assert.DoesNotContain(entry, property => property.Key.StartsWith("Has", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CheckRegisterRecipe_ImportsChecksAsHistory_AndSkipsOnesAlreadyThere()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var existing = await context.IssueAsync(account);
        var step = new JsonObject
        {
            ["name"] = "CheckRegister",
            [CheckRegisterRecipeStep.ChecksProperty] = new JsonArray(
                new JsonObject
                {
                    ["bankAccountId"] = account.ItemId,
                    ["number"] = 512,
                    ["payeeName"] = "Old payee",
                    ["amount"] = 75.25,
                    ["currency"] = "USD",
                    ["checkDate"] = "2025-06-30",
                    ["status"] = "Cleared",
                },
                new JsonObject
                {
                    ["bankAccountId"] = account.ItemId,
                    ["number"] = existing.Number,
                    ["payeeName"] = "Duplicate",
                    ["amount"] = 1,
                }),
        };

        // Act
        var errors = await RecipeRoundTrip.RunAsync(new CheckRegisterRecipeStep(context.Manager), step);

        // Assert
        Assert.Empty(errors);
        Assert.Equal(2, context.Store.Entries.Count);
        var imported = Assert.Single(context.Store.Entries, entry => entry.Number == 512);
        Assert.True(imported.IsHistorical);
        Assert.Equal(CheckStatus.Cleared, imported.Status);
        Assert.Equal(new DateOnly(2025, 6, 30), imported.CheckDate);
        Assert.False(await context.Service.VoidAsync(imported, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void PositivePayRecipe_ReadsOnlyFieldsTheCheckRegisterDataSetHas()
    {
        // Arrange
        var path = Path.Combine(FindRepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.Banking", "Recipes", "checks-positive-pay.recipe.json");
        var recipe = JsonNode.Parse(File.ReadAllText(path))!;
        var dataSet = new CheckRegisterReportDataSet(Mock.Of<ISession>(), Mock.Of<IAuthorizationService>(), new SourceAccessService([]), RecipeRoundTrip.Localizer<CheckRegisterReportDataSet>());
        var fields = dataSet.Fields.Select(field => field.Name).ToHashSet(StringComparer.Ordinal);

        // Act
        var pipelines = recipe["steps"]!.AsArray().Single(step => step!["name"]!.GetValue<string>() == "DataPipelines")!["Pipelines"]!.AsArray();
        var source = pipelines.Single()!["Definition"]!["Steps"]!.AsArray().Single(step => step!["Type"]!.GetValue<string>() == "DataSource")!["Properties"]!["DataSourceStepSettings"]!;

        // Assert
        Assert.Equal(ChecksReportDataSource.Name, source["Source"]!.GetValue<string>());
        Assert.Equal(CheckRegisterReportDataSet.DataSetName, source["DataSet"]!.GetValue<string>());
        Assert.All(source["Fields"]!.AsArray(), field => Assert.Contains(field!.GetValue<string>(), fields));
        Assert.Contains(source["IncrementalField"]!.GetValue<string>(), fields);
        Assert.DoesNotContain(fields, name => name.Contains("AccountNumber", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
