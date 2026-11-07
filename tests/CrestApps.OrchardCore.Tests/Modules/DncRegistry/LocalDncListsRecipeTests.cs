using System.Text.Json.Nodes;
using CrestApps.OrchardCore.DncRegistry.Deployments;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.Recipes;
using CrestApps.OrchardCore.DncRegistry.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.DncRegistry;

/// <summary>
/// Local do-not-call lists travel between sites: the imported lists are exported with their numbers, and the recipe
/// step queues each one through the same import as an uploaded file, once, so a second run adds nothing.
/// </summary>
public sealed class LocalDncListsRecipeTests
{
    [Fact]
    public async Task ImportedLists_ExportWithTheirNumbers_AndQueueOnceOnAnotherSite()
    {
        // Arrange
        var source = new Mock<ILocalDncListManager>();

        source.Setup(manager => manager.GetListsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new LocalDncList { ListId = "list-1", Name = "Opted out", CountryCode = "US", Status = LocalDncListStatus.Completed },
                new LocalDncList { ListId = "list-2", Name = "Still importing", CountryCode = "US", Status = LocalDncListStatus.Processing },
            ]);
        source.Setup(manager => manager.GetPhoneNumbersAsync("list-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["+12025550101", "+12025550102"]);

        var target = new FakeListManager();
        var step = new LocalDncListsStep(target.Object, RecipeRoundTrip.Localizer<LocalDncListsStep>());

        // Act
        var exported = await RecipeRoundTrip.ExportAsync(new LocalDncListsDeploymentSource(source.Object), new LocalDncListsDeploymentStep());
        var first = await RecipeRoundTrip.RunAsync(step, exported);
        var second = await RecipeRoundTrip.RunAsync(step, exported);

        // Assert
        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Equal("LocalDncLists", exported["name"].GetValue<string>());

        var queued = Assert.Single(target.Queued);

        Assert.Equal("Opted out", queued.Name);
        Assert.Equal("US", queued.CountryCode);
        Assert.Equal(["PhoneNumber", "+12025550101", "+12025550102"], queued.File.Split('\n'));
    }

    [Fact]
    public async Task AListWithoutNumbers_IsReported()
    {
        // Arrange
        var target = new FakeListManager();
        var step = new LocalDncListsStep(target.Object, RecipeRoundTrip.Localizer<LocalDncListsStep>());
        var recipe = JsonNode.Parse("""{ "name": "LocalDncLists", "Lists": [ { "Name": "Empty", "CountryCode": "us", "PhoneNumbers": [] } ] }""").AsObject();

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Equal(["The local DNC list 'Empty' has no phone numbers."], errors);
        Assert.Empty(target.Queued);
    }

    private sealed class FakeListManager : Mock<ILocalDncListManager>
    {
        public FakeListManager()
        {
            Setup(manager => manager.GetListsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string countryCode, CancellationToken _) => Queued
                    .Where(list => countryCode is null || list.CountryCode == countryCode)
                    .Select(list => new LocalDncList { Name = list.Name, CountryCode = list.CountryCode })
                    .ToList());
            Setup(manager => manager.QueueImportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns(async (string name, string countryCode, string fileName, Stream stream, CancellationToken cancellationToken) =>
                {
                    using var reader = new StreamReader(stream);

                    Queued.Add((name, countryCode, await reader.ReadToEndAsync(cancellationToken)));

                    return new LocalDncList { Name = name, CountryCode = countryCode };
                });
        }

        public List<(string Name, string CountryCode, string File)> Queued { get; } = [];
    }
}
