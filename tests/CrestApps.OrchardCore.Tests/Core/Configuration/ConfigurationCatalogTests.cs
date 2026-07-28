using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Configuration;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Core.Configuration;

/// <summary>
/// Covers the rules the shared configuration catalog has to honour when a plan is imported, in isolation from any
/// particular module's managers.
/// </summary>
public sealed class ConfigurationCatalogTests
{
    /// <summary>
    /// A manager is entitled to derive a value from the entry it is given - to canonicalise it, to compute it, or to
    /// resolve it against something only the manager can see. Writing the plan's raw value back over the derived one
    /// would replace what the module decided the entry means with what the file happened to say, so the same entry
    /// would import differently from the way it is created through the manager.
    /// </summary>
    [Fact]
    public async Task ImportingAnEntry_DoesNotOverwriteAValueTheManagerDerived()
    {
        // Arrange
        var manager = CreateManager();

        manager
            .Setup(x => x.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((JsonNode data, CancellationToken _) => ValueTask.FromResult(new TestConfigurationEntry
            {
                ItemId = "4bvk8hph7xb84tz7etqea1035w",
                Description = $"{data?["Name"]?.GetValue<string>()}-derived",
            }));

        TestConfigurationEntry created = null;

        manager
            .Setup(x => x.CreateAsync(It.IsAny<TestConfigurationEntry>(), It.IsAny<CancellationToken>()))
            .Callback((TestConfigurationEntry entry, CancellationToken _) => created = entry)
            .Returns(ValueTask.CompletedTask);

        var step = CreateStep(manager.Object);

        var context = CreateContext(new JsonObject
        {
            ["Name"] = "Support",
            ["Description"] = "whatever the file happened to say",
        });

        // Act
        await step.ExecuteAsync(context);

        // Assert
        Assert.Empty(context.Errors);
        Assert.NotNull(created);

        Assert.Equal("Support-derived", created.Description);
        Assert.Equal("Support", created.Name);
    }

    /// <summary>
    /// An entry the module refuses is an entry the module will not serve. Persisting it and only then asking whether
    /// it was acceptable leaves the tenant holding configuration that failed its own validation.
    /// </summary>
    [Fact]
    public async Task ImportingAnInvalidEntry_ReportsTheErrorWithoutPersistingIt()
    {
        // Arrange
        var manager = CreateManager();

        manager
            .Setup(x => x.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new TestConfigurationEntry { ItemId = "4erxm680hgje2rhnpvrqztazc6" }));

        var rejection = new ValidationResultDetails();

        rejection.Fail(new ValidationResult("A name is required.", ["Name"]));

        manager
            .Setup(x => x.ValidateAsync(It.IsAny<TestConfigurationEntry>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(rejection));

        var step = CreateStep(manager.Object);

        var context = CreateContext(new JsonObject());

        // Act
        await step.ExecuteAsync(context);

        // Assert
        Assert.Contains("A name is required.", context.Errors);

        manager.Verify(x => x.CreateAsync(It.IsAny<TestConfigurationEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        manager.Verify(x => x.UpdateAsync(It.IsAny<TestConfigurationEntry>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// One malformed entry is a mistake in one entry. Letting it abort the step leaves the operator with a tenant
    /// configured up to the point of the failure and no account of what the rest of the plan would have done.
    /// </summary>
    [Fact]
    public async Task AnEntryThatCannotBeBuilt_IsReportedWithoutStoppingTheEntriesAroundIt()
    {
        // Arrange
        var manager = CreateManager();

        manager
            .Setup(x => x.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((JsonNode data, CancellationToken _) =>
            {
                if (data?["Name"]?.GetValue<string>() == "broken")
                {
                    throw new InvalidOperationException("The source is required.");
                }

                return ValueTask.FromResult(new TestConfigurationEntry { ItemId = "4znpg5ybpcckmtawz3dhxr2mxc" });
            });

        manager
            .Setup(x => x.CreateAsync(It.IsAny<TestConfigurationEntry>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var step = CreateStep(manager.Object);

        var context = CreateContext(
            new JsonObject { ["Name"] = "broken" },
            new JsonObject { ["Name"] = "sound" });

        // Act
        await step.ExecuteAsync(context);

        // Assert
        Assert.NotEmpty(context.Errors);

        manager.Verify(x => x.CreateAsync(It.IsAny<TestConfigurationEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A hand-written plan is a supported way to script a tenant, and its author has no store-issued identifiers to
    /// use, so entries are named whatever reads well and referenced by that name. The store still issues the real
    /// identifier, so what the plan called an entry has to be translated wherever the plan used it, or a scripted
    /// tenant lands with every cross-reference pointing at nothing.
    /// </summary>
    [Fact]
    public async Task AHandWrittenPlan_TranslatesTheIdentifiersItInvented()
    {
        // Arrange
        var manager = CreateManager();

        // The store issues the identifier, so the plan's own naming never reaches it.
        manager
            .Setup(x => x.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((JsonNode data, CancellationToken _) => ValueTask.FromResult(new TestConfigurationEntry
            {
                ItemId = data?["Name"]?.GetValue<string>() == "Support"
                    ? "4n5adcjbrwy0w4xygk2zaqwfds"
                    : "4zm0xvghj9pjwrhb6ytg2b4pmc",
            }));

        var created = new List<TestConfigurationEntry>();

        manager
            .Setup(x => x.CreateAsync(It.IsAny<TestConfigurationEntry>(), It.IsAny<CancellationToken>()))
            .Callback((TestConfigurationEntry entry, CancellationToken _) => created.Add(entry))
            .Returns(ValueTask.CompletedTask);

        manager
            .Setup(x => x.UpdateAsync(It.IsAny<TestConfigurationEntry>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        manager
            .Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) =>
                ValueTask.FromResult(created.Find(candidate => candidate.ItemId == id)));

        var step = CreateStep(manager.Object);

        var context = CreateContext(
            new JsonObject
            {
                ["ItemId"] = "support-queue",
                ["Name"] = "Support",
            },
            new JsonObject
            {
                ["ItemId"] = "overflow-queue",
                ["Name"] = "Overflow",
                ["Description"] = "support-queue",
            });

        // Act
        await step.ExecuteAsync(context);

        // Assert
        Assert.Empty(context.Errors);
        Assert.Equal(2, created.Count);

        var overflow = created.Find(candidate => candidate.Name == "Overflow");

        Assert.Equal("4n5adcjbrwy0w4xygk2zaqwfds", overflow.Description);
    }

    /// <summary>
    /// Nothing in the product stops an operator from keeping two entries under one name, and reconciling a plan by
    /// name is only safe if each entry the destination already owned is claimed once. Letting a second entry in the
    /// same plan match an entry the plan itself just created overwrites the first with the second, so two entries an
    /// operator deliberately kept apart arrive as one and every reference to the lost entry is quietly redirected.
    /// </summary>
    [Fact]
    public async Task TwoEntriesThatShareAName_AreBothImported()
    {
        // Arrange
        var manager = CreateManager();

        var created = new List<TestConfigurationEntry>();

        manager
            .Setup(x => x.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new TestConfigurationEntry
            {
                ItemId = created.Count == 0
                    ? "4h2vkrfy0qz9v4bpdmwc7t8x1r"
                    : "4t7wq3nzc8mgy1kvj5bs6dprea",
            }));

        manager
            .Setup(x => x.CreateAsync(It.IsAny<TestConfigurationEntry>(), It.IsAny<CancellationToken>()))
            .Callback((TestConfigurationEntry entry, CancellationToken _) => created.Add(entry))
            .Returns(ValueTask.CompletedTask);

        var step = CreateStep(manager.Object);

        var context = CreateContext(
            new JsonObject
            {
                ["Name"] = "Support",
                ["Description"] = "first",
            },
            new JsonObject
            {
                ["Name"] = "Support",
                ["Description"] = "second",
            });

        // Act
        await step.ExecuteAsync(context);

        // Assert
        Assert.Empty(context.Errors);
        Assert.Equal(2, created.Count);

        manager.Verify(x => x.UpdateAsync(It.IsAny<TestConfigurationEntry>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// A hand-written plan that names its collection something the tenant does not recognise imports none of that
    /// configuration. Reporting the step as executed hands the operator a tenant that looks configured because the
    /// plan ran without complaint, which is the failure a plan is supposed to protect against.
    /// </summary>
    [Fact]
    public async Task AStepThatNamesNoCollectionTheTenantRecognises_IsReportedRatherThanPassedOver()
    {
        // Arrange
        var manager = CreateManager();

        var step = CreateStep(manager.Object);

        var context = new RecipeExecutionContext
        {
            Name = "TestConfiguration",
            Step = new JsonObject
            {
                ["name"] = "TestConfiguration",
                ["Entires"] = new JsonArray(new JsonObject { ["Name"] = "Support" }),
            },
        };

        // Act
        await step.ExecuteAsync(context);

        // Assert
        Assert.NotEmpty(context.Errors);

        manager.Verify(x => x.CreateAsync(It.IsAny<TestConfigurationEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The recipe engine binds its own steps without regard to case, so a plan that spells a collection the way it
    /// reads has always worked. Matching the collection exactly would silently drop that configuration.
    /// </summary>
    [Fact]
    public async Task AStepThatSpellsItsCollectionInAnotherCase_IsStillImported()
    {
        // Arrange
        var manager = CreateManager();

        manager
            .Setup(x => x.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new TestConfigurationEntry { ItemId = "4kq8zn3rvxc7g2ym5b9dtwphes" }));

        manager
            .Setup(x => x.CreateAsync(It.IsAny<TestConfigurationEntry>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var step = CreateStep(manager.Object);

        var context = new RecipeExecutionContext
        {
            Name = "TestConfiguration",
            Step = new JsonObject
            {
                ["name"] = "TestConfiguration",
                ["entries"] = new JsonArray(new JsonObject { ["Name"] = "Support" }),
            },
        };

        // Act
        await step.ExecuteAsync(context);

        // Assert
        Assert.Empty(context.Errors);

        manager.Verify(x => x.CreateAsync(It.IsAny<TestConfigurationEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<ICatalogManager<TestConfigurationEntry>> CreateManager()    {
        var manager = new Mock<ICatalogManager<TestConfigurationEntry>>();

        manager
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IEnumerable<TestConfigurationEntry>>([]));

        manager
            .Setup(x => x.ValidateAsync(It.IsAny<TestConfigurationEntry>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        return manager;
    }

    private static RecipeExecutionContext CreateContext(params JsonObject[] entries)
    {
        var array = new JsonArray();

        foreach (var entry in entries)
        {
            array.Add(entry);
        }

        return new RecipeExecutionContext
        {
            Name = "TestConfiguration",
            Step = new JsonObject
            {
                ["name"] = "TestConfiguration",
                ["Entries"] = array,
            },
        };
    }

    private static ConfigurationCatalogRecipeStep CreateStep(ICatalogManager<TestConfigurationEntry> manager)
    {
        var catalog = new ConfigurationCatalog<TestConfigurationEntry>(
            manager,
            new ConfigurationCatalogDescriptor
            {
                Group = "Tests",
                StepName = "TestConfiguration",
                CollectionName = "Entries",
                Order = 10,
            },
            new ConfigurationImportIdentityStore(new Clock()));

        return new ConfigurationCatalogRecipeStep([catalog]);
    }
}
