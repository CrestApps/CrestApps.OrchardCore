using System.Text.Json.Nodes;
using CrestApps.OrchardCore.BusinessDocuments.Core.Services;
using CrestApps.OrchardCore.BusinessDocuments.Deployments;
using CrestApps.OrchardCore.BusinessDocuments.Handlers;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.Recipes;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Tests.BusinessDocuments;

/// <summary>
/// Document templates travel between sites: an export imports into an empty store with the same ids and values, a
/// second run updates instead of duplicating, and a template the rules refuse is reported instead of stored.
/// </summary>
public sealed class BusinessDocumentTemplatesPortabilityTests
{
    [Fact]
    public async Task Templates_ExportAndImportIntoAnEmptyStore_WithoutDuplicatesOnASecondRun()
    {
        // Arrange
        var source = new InMemoryBusinessDocumentTemplateStore();

        source.Entries.Add(new BusinessDocumentTemplate
        {
            ItemId = "4bdt0000000000000000000001",
            Name = "Standard",
            Description = "The look of every invoice.",
            Kinds = ["Invoice", "Estimate"],
            IsDefault = true,
            LogoUrl = "/media/logo.png",
            AccentColor = "#2563EB",
            PaperSize = BusinessDocumentPaperSize.A4,
            Footer = "Registered in Springfield.",
            OwnerId = "source-user",
            Author = "admin",
            CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });

        var target = new InMemoryBusinessDocumentTemplateStore();
        var step = new BusinessDocumentTemplatesStep(CreateManager(target));

        // Act
        var exported = await RecipeRoundTrip.ExportAsync(new BusinessDocumentTemplatesDeploymentSource(CreateManager(source)), new BusinessDocumentTemplatesDeploymentStep());
        var first = await RecipeRoundTrip.RunAsync(step, exported);
        var second = await RecipeRoundTrip.RunAsync(step, exported);

        // Assert
        Assert.Equal(BusinessDocumentTemplatesDeploymentStep.StepName, exported["name"].GetValue<string>());

        var entry = Assert.Single(exported[BusinessDocumentTemplatesDeploymentStep.EntriesName].AsArray()).AsObject();

        Assert.False(entry.ContainsKey(nameof(BusinessDocumentTemplate.OwnerId)));
        Assert.False(entry.ContainsKey(nameof(BusinessDocumentTemplate.CreatedUtc)));
        Assert.Empty(first);
        Assert.Empty(second);

        var template = Assert.Single(target.Entries);

        Assert.Equal("4bdt0000000000000000000001", template.ItemId);
        Assert.Equal("Standard", template.Name);
        Assert.Equal("The look of every invoice.", template.Description);
        Assert.Equal(new[] { "Invoice", "Estimate" }, template.Kinds);
        Assert.True(template.IsDefault);
        Assert.Equal("/media/logo.png", template.LogoUrl);
        Assert.Equal("#2563EB", template.AccentColor);
        Assert.Equal(BusinessDocumentPaperSize.A4, template.PaperSize);
        Assert.Equal("Registered in Springfield.", template.Footer);
    }

    [Fact]
    public async Task Templates_ARecipeWithAnInvalidAccentColor_IsReportedAndNotStored()
    {
        // Arrange
        var target = new InMemoryBusinessDocumentTemplateStore();
        var step = new BusinessDocumentTemplatesStep(CreateManager(target));
        var recipe = new JsonObject
        {
            ["name"] = BusinessDocumentTemplatesDeploymentStep.StepName,
            [BusinessDocumentTemplatesDeploymentStep.EntriesName] = new JsonArray(new JsonObject
            {
                ["Name"] = "Broken",
                ["AccentColor"] = "blue",
            }),
        };

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.NotEmpty(errors);
        Assert.Empty(target.Entries);
    }

    private static BusinessDocumentTemplateManager CreateManager(InMemoryBusinessDocumentTemplateStore store)
        => store.CreateManager(new BusinessDocumentTemplateHandler(new HttpContextAccessor(), new TestClock(DateTime.UtcNow), new PassThroughStringLocalizer<BusinessDocumentTemplateHandler>()));
}
