using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Core.Telemetry;
using CrestApps.OrchardCore.Recipes.Core.Services;
using CrestApps.OrchardCore.Recipes.Core.Telemetry;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.FileStorage;
using OrchardCore.Json;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Telemetry.Recipes;

/// <summary>
/// Recipe executions, the recipe steps of named catalogs, stock count imports and stock changes emit the
/// <c>crestapps.recipes.*</c> and <c>crestapps.inventory.*</c> instruments and spans the observability docs list, tagged
/// with bounded values only. The names are a public contract that dashboards and alerts depend on.
/// </summary>
public sealed class RecipesAndInventoryTelemetryTests
{
    private static readonly ActivitySource _testSource = new("CrestApps.OrchardCore.Tests.Recipes");

    [Fact]
    public async Task ExecuteRecipeAsync_EmitsTheExecutionSpanWithAChildPerHandler_AndTheExecutionMetrics()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var handler = new Mock<IDeploymentTargetHandler>();
        handler.Setup(x => x.ImportFromFileAsync(It.IsAny<IFileProvider>())).Returns(Task.CompletedTask);
        var service = CreateRecipeExecutionService(handler.Object);
        var recipe = new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["name"] = "Feature" }, new JsonObject { ["name"] = "Settings" }) };

        // Act
        bool executed;

        using (var parent = _testSource.StartActivity("test"))
        {
            executed = await service.ExecuteRecipeAsync(recipe);
        }

        // Assert
        Assert.True(executed);

        var execute = Assert.Single(capture.Spans, span => span.OperationName == RecipesTelemetry.ExecuteSpanName && span.Parent?.Source == _testSource);
        Assert.Equal(2, execute.GetTagItem(RecipesTelemetry.StepCountTag));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeOk, execute.GetTagItem(CrestAppsOrchardCoreTelemetry.OutcomeTag));

        var child = Assert.Single(capture.Spans, span => span.OperationName == RecipesTelemetry.DeploymentTargetSpanName && span.ParentSpanId == execute.SpanId);
        Assert.NotNull(child.GetTagItem(RecipesTelemetry.HandlerTag));

        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.recipes.execution.duration"
            && Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag], CrestAppsOrchardCoreTelemetry.OutcomeOk));
        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.recipes.executions"
            && Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag], CrestAppsOrchardCoreTelemetry.OutcomeOk));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public async Task ExecuteRecipeAsync_WhenAHandlerThrows_RecordsTheErrorOnBothSpansAndTheExecutions()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var handler = new Mock<IDeploymentTargetHandler>();
        handler.Setup(x => x.ImportFromFileAsync(It.IsAny<IFileProvider>())).ThrowsAsync(new InvalidOperationException("boom"));
        var service = CreateRecipeExecutionService(handler.Object);

        // Act
        bool executed;

        using (var parent = _testSource.StartActivity("test"))
        {
            executed = await service.ExecuteRecipeAsync(new JsonObject { ["steps"] = new JsonArray() });
        }

        // Assert
        Assert.False(executed);

        var execute = Assert.Single(capture.Spans, span => span.OperationName == RecipesTelemetry.ExecuteSpanName && span.Parent?.Source == _testSource);
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeError, execute.GetTagItem(CrestAppsOrchardCoreTelemetry.OutcomeTag));
        Assert.Equal(ActivityStatusCode.Error, execute.Status);

        var child = Assert.Single(capture.Spans, span => span.OperationName == RecipesTelemetry.DeploymentTargetSpanName && span.ParentSpanId == execute.SpanId);
        Assert.Equal(ActivityStatusCode.Error, child.Status);

        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.recipes.executions"
            && Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag], CrestAppsOrchardCoreTelemetry.OutcomeError));
    }

    [Fact]
    public async Task NamedCatalogRecipeStep_CountsItsEntriesByWhatBecameOfThem()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var stepName = "TelemetryStep" + Guid.NewGuid().ToString("n");
        var existing = new CatalogEntry { ItemId = "existing", Name = "Existing" };
        var manager = new Mock<INamedCatalogManager<CatalogEntry>>();

        manager.Setup(x => x.FindByIdAsync("existing", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        manager.Setup(x => x.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((JsonNode data, CancellationToken _) =>
            {
                var name = data["Name"]?.GetValue<string>();

                return ValueTask.FromResult(name == "skip" ? null : new CatalogEntry { Name = name });
            });
        manager.Setup(x => x.ValidateAsync(It.IsAny<CatalogEntry>(), It.IsAny<CancellationToken>()))
            .Returns((CatalogEntry entry, CancellationToken _) =>
            {
                var result = new ValidationResultDetails();

                if (entry.Name == "bad")
                {
                    result.Fail(new ValidationResult("The name is not allowed."));
                }

                return ValueTask.FromResult(result);
            });

        var step = new TelemetryRecipeStep(stepName, manager.Object);
        var context = new RecipeExecutionContext
        {
            Name = stepName,
            Step = new JsonObject
            {
                ["name"] = stepName,
                ["Entries"] = new JsonArray(
                    new JsonObject { ["Name"] = "created" },
                    new JsonObject { ["ItemId"] = "existing", ["Name"] = "Renamed" },
                    new JsonObject { ["Name"] = "bad" },
                    new JsonObject { ["Name"] = "skip" }),
            },
        };

        // Act
        await step.ExecuteAsync(context);

        // Assert
        Assert.Single(context.Errors);

        var entries = capture.Find("crestapps.recipes.step.entries", "step", stepName);
        Assert.Equal(1, entries.Single(entry => Equals(entry.Tags[CrestAppsOrchardCoreTelemetry.ResultTag], "created")).Value);
        Assert.Equal(1, entries.Single(entry => Equals(entry.Tags[CrestAppsOrchardCoreTelemetry.ResultTag], "updated")).Value);
        Assert.Equal(1, entries.Single(entry => Equals(entry.Tags[CrestAppsOrchardCoreTelemetry.ResultTag], "invalid")).Value);
        Assert.Equal(1, entries.Single(entry => Equals(entry.Tags[CrestAppsOrchardCoreTelemetry.ResultTag], "skipped")).Value);

        var stepSpan = Assert.Single(capture.Spans, span => span.OperationName == "crestapps.recipes.step" && Equals(span.GetTagItem("crestapps.recipes.step"), stepName));
        Assert.Equal(4, stepSpan.GetTagItem("crestapps.recipes.entries"));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeOk, stepSpan.GetTagItem(CrestAppsOrchardCoreTelemetry.OutcomeTag));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public async Task StockCountImporter_RecordsTheImportSpanDurationAndRowsByResult()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var counted = new StockItem { ItemId = "counted", Sku = "A", OnHand = 1 };
        var unchanged = new StockItem { ItemId = "unchanged", Sku = "B", OnHand = 5 };
        var items = new Mock<IStockItemStore>();

        items.Setup(x => x.FindBySkuAsync("A", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(counted);
        items.Setup(x => x.FindBySkuAsync("B", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(unchanged);

        var inventory = new Mock<IInventoryService>();
        inventory.Setup(x => x.SetCountAsync("counted", 3, It.IsAny<string>(), It.IsAny<StockActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StockChangeResult.Success(counted));

        var importer = new StockCountImporter(items.Object, inventory.Object, NullLogger<StockCountImporter>.Instance);

        // Act
        StockCountImportResult result;

        using (var parent = _testSource.StartActivity("test"))
        {
            result = await importer.ImportAsync(
            [
                new StockCountRow { Line = 2, Sku = "A", OnHand = 3 },
                new StockCountRow { Line = 3, Sku = "B", OnHand = 5 },
                new StockCountRow { Line = 4, Sku = "MISSING", OnHand = 1 },
                new StockCountRow { Line = 5, Sku = "A", OnHand = 4 },
                new StockCountRow { Line = 6, Sku = "B" },
                new StockCountRow { Line = 7, TooMany = true },
            ], "Counted.", new StockActor("user", "User"), TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Equal(1, result.Counted);
        Assert.Equal(1, result.Unchanged);

        var importSpan = Assert.Single(capture.Spans, span => span.OperationName == InventoryTelemetry.StockCountSpanName && span.Parent?.Source == _testSource);
        Assert.Equal(InventoryTelemetry.OutcomeTruncated, importSpan.GetTagItem(CrestAppsOrchardCoreTelemetry.OutcomeTag));
        Assert.Equal(1, importSpan.GetTagItem("crestapps.inventory.rows.counted"));
        Assert.Equal(1, importSpan.GetTagItem("crestapps.inventory.rows.unchanged"));
        Assert.Equal(3, importSpan.GetTagItem("crestapps.inventory.rows.not_applied"));

        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.inventory.stock_count.duration"
            && Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag], InventoryTelemetry.OutcomeTruncated));

        foreach (var expected in new[] { "counted", "unchanged", "not_found", "duplicate", "invalid_count" })
        {
            Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.inventory.stock_count.rows"
                && Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.ResultTag], expected));
        }

        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public void InventoryTelemetry_RecordsMovementsAndHoldsWithBoundedTags()
    {
        // Arrange
        using var capture = new TelemetryCapture();

        // Act
        InventoryTelemetry.RecordMovement(StockMovementKind.Sold, -2);
        InventoryTelemetry.RecordMovement(StockMovementKind.Received, 7);
        InventoryTelemetry.RecordHolds(InventoryTelemetry.HoldExpired, 3);
        InventoryTelemetry.RecordHoldRefusal();

        // Assert
        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.inventory.movements"
            && Equals(measurement.Tags[InventoryTelemetry.TypeTag], "sold")
            && Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.DirectionTag], "out"));
        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.inventory.movement.units"
            && Equals(measurement.Tags[InventoryTelemetry.TypeTag], "received")
            && Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.DirectionTag], "in")
            && measurement.Value == 7);
        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.inventory.holds"
            && Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag], InventoryTelemetry.HoldExpired)
            && measurement.Value == 3);
        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.inventory.hold.refusals");
        Assert.Equal("other", InventoryTelemetry.GetMovementType((StockMovementKind)99));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    private static RecipeExecutionService CreateRecipeExecutionService(IDeploymentTargetHandler handler)
    {
        var tempDirectoryProvider = new Mock<ITempDirectoryProvider>();
        tempDirectoryProvider
            .Setup(provider => provider.GetTempFileName(It.IsAny<string>()))
            .Returns((string extension) => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + (extension ?? string.Empty)));

        return new RecipeExecutionService(
            [handler],
            Options.Create(new DocumentJsonSerializerOptions()),
            tempDirectoryProvider.Object,
            NullLogger<RecipeExecutionService>.Instance);
    }

    /// <summary>
    /// A catalog entry the test recipe step imports.
    /// </summary>
    public sealed class CatalogEntry : CatalogItem, INameAwareModel
    {
        /// <summary>
        /// Gets or sets the name of the entry.
        /// </summary>
        public string Name { get; set; }
    }

    private sealed class TelemetryRecipeStep : NamedCatalogRecipeStep<CatalogEntry>
    {
        public TelemetryRecipeStep(string stepName, INamedCatalogManager<CatalogEntry> manager)
            : base(stepName, "Entries", manager)
        {
        }
    }
}
