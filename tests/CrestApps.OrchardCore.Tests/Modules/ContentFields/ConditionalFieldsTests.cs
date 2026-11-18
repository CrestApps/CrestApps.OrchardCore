using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.ContentFields.ConditionalFields;
using CrestApps.OrchardCore.ContentFields.Conditions;
using CrestApps.OrchardCore.ContentFields.Settings;
using CrestApps.OrchardCore.ContentFields.Taxonomies;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Tests.Modules.ContentFields;

/// <summary>
/// Fields that show only when other answers say so: what a hidden field holds after the post, its validation, and the
/// readers rules use.
/// </summary>
public sealed class ConditionalFieldsTests
{
    private static readonly ConditionRule _whenOutage = new()
    {
        Conditions = [new Condition { Source = "Request.Outage", Operator = ConditionOperator.Is, Values = ["true"] }],
    };

    [Fact]
    public async Task OnCreate_AValuePostedForAHiddenFieldIsDropped_AndItsRequiredErrorRemoved()
    {
        // Arrange: "Who else is affected?" shows only for an outage; the post says no outage but sends an answer.
        var kit = new Kit();
        var item = Kit.NewItem();

        kit.Snapshots.Take(item);
        Post(item, outage: false, affected: "forged");
        var context = Kit.Context();
        context.Updater.ModelState.AddModelError("Request.Affected.Text", "A value is required for Affected.");
        context.Updater.ModelState.AddModelError("Request.Model.Text", "A value is required for Model.");

        // Act
        await kit.Handler.UpdateEditorAsync(item, context);

        // Assert
        Assert.Null(((JsonObject)item.Content)["Request"]?["Affected"]);
        Assert.False(context.Updater.ModelState.ContainsKey("Request.Affected.Text"));
        Assert.True(context.Updater.ModelState.ContainsKey("Request.Model.Text"));
    }

    [Fact]
    public async Task OnEdit_AHiddenFieldKeepsItsStoredValue()
    {
        // Arrange: the stored item had an outage and an answer; the edit turns the outage off and changes the answer.
        var kit = new Kit();
        var item = Kit.NewItem();
        Post(item, outage: true, affected: "Finance");

        kit.Snapshots.Take(item);
        Post(item, outage: false, affected: "Everyone");

        // Act
        await kit.Handler.UpdateEditorAsync(item, Kit.Context());

        // Assert: nothing is lost if the outage comes back, and the posted value wasn't kept.
        Assert.Equal("Finance", ((JsonObject)item.Content)["Request"]?["Affected"]?["Text"]?.GetValue<string>());
        Assert.False(((JsonObject)item.Content)["Request"]?["Outage"]?["Value"]?.GetValue<bool>());
    }

    [Fact]
    public async Task AShownField_KeepsWhatWasPosted_AndItsErrors()
    {
        // Arrange
        var kit = new Kit();
        var item = Kit.NewItem();

        kit.Snapshots.Take(item);
        Post(item, outage: true, affected: "Finance");
        var context = Kit.Context();
        context.Updater.ModelState.AddModelError("Request.Affected.Text", "Too long.");

        // Act
        await kit.Handler.UpdateEditorAsync(item, context);

        // Assert
        Assert.Equal("Finance", ((JsonObject)item.Content)["Request"]?["Affected"]?["Text"]?.GetValue<string>());
        Assert.True(context.Updater.ModelState.ContainsKey("Request.Affected.Text"));
    }

    [Fact]
    public async Task AFieldAVisibilityProviderHides_IsTreatedAsHidden()
    {
        // Arrange: a provider hides "Model" (as the customer's form hides internal fields), which is required.
        var provider = new Mock<IContentFieldVisibilityProvider>();
        provider.Setup(item => item.GetHiddenFieldsAsync(It.IsAny<ContentItem>(), It.IsAny<ContentTypeDefinition>())).ReturnsAsync(["Request.Model"]);
        var kit = new Kit(provider.Object);
        var item = Kit.NewItem();

        kit.Snapshots.Take(item);
        ((JsonObject)item.Content)["Request"]!["Model"] = new JsonObject { ["Text"] = "posted" };
        var context = Kit.Context();
        context.Updater.ModelState.AddModelError("Request.Model.Text", "A value is required for Model.");

        // Act
        await kit.Handler.UpdateEditorAsync(item, context);

        // Assert
        Assert.Null(((JsonObject)item.Content)["Request"]?["Model"]);
        Assert.Empty(context.Updater.ModelState);
    }

    [Fact]
    public async Task AnEditorWithAPrefix_RemovesTheErrorsUnderItsPrefix()
    {
        // Arrange: a contained item's editor (a bag, a flow) prefixes its fields.
        var kit = new Kit();
        var item = Kit.NewItem();

        kit.Snapshots.Take(item);
        Post(item, outage: false, affected: "forged");
        var context = Kit.Context("Items[0]");
        context.Updater.ModelState.AddModelError("Items[0].Request.Affected.Text", "Required.");

        // Act
        await kit.Handler.UpdateEditorAsync(item, context);

        // Assert
        Assert.Empty(context.Updater.ModelState);
    }

    [Fact]
    public async Task WithoutASnapshot_NothingIsTouched()
    {
        // Arrange: an item whose type has no rules and no provider never gets a snapshot.
        var kit = new Kit();
        var item = Kit.NewItem();
        Post(item, outage: false, affected: "kept");

        // Act
        await kit.Handler.UpdateEditorAsync(item, Kit.Context());

        // Assert
        Assert.Equal("kept", ((JsonObject)item.Content)["Request"]?["Affected"]?["Text"]?.GetValue<string>());
    }

    [Fact]
    public async Task TaxonomyReader_GivesTheTermsAboveTheChosenOne()
    {
        // Arrange: Hardware > Laptop.
        var taxonomy = Taxonomy();
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync("tax", VersionOptions.Published)).ReturnsAsync(taxonomy);
        var reader = new TaxonomyFieldValueReader(contentManager.Object);
        var definition = new ContentPartFieldDefinition(new ContentFieldDefinition("TaxonomyField"), "Category", new JsonObject
        {
            ["TaxonomyFieldSettings"] = new JsonObject { ["TaxonomyContentItemId"] = "tax" },
        });

        // Act
        var value = await reader.ReadAsync(new JsonObject { ["TermContentItemIds"] = new JsonArray("laptop") }, definition);
        var descriptor = new ContentConditionSourceDescriptor();
        await reader.DescribeAsync(definition, descriptor);

        // Assert
        Assert.Equal(["laptop"], value.Values);
        Assert.Equal(["hardware"], value.Ancestors);
        Assert.True(ConditionEvaluator.IsMet(new Condition { Operator = ConditionOperator.IsOrUnder, Values = ["hardware"], Source = "x" }, value));
        Assert.True(descriptor.IsHierarchical);
        Assert.Equal(["hardware", "laptop", "printer"], descriptor.Options.Select(option => option.Value));
        Assert.Equal(1, descriptor.Options.Single(option => option.Value == "laptop").Level);
        Assert.Equal(["hardware"], descriptor.Ancestors["laptop"]);
    }

    [Fact]
    public void TermTree_NamesAPathFromTheTop()
    {
        // Act
        var tree = TaxonomyTermTree.From(Taxonomy());

        // Assert
        Assert.Equal("Hardware > Laptop", tree.GetPathText("laptop"));
        Assert.Equal(["laptop", "printer"], tree.ChildrenOf("hardware").Select(term => term.Id));
        Assert.Null(tree.GetPathText("gone"));
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void JsonValues_ReadInTheInvariantCulture(bool flag, string expected)
    {
        Assert.Equal([expected], JsonFieldValueReader.ToStrings(JsonValue.Create(flag)));
        Assert.Equal(["12.5"], JsonFieldValueReader.ToStrings(JsonValue.Create(12.5m)));
        Assert.Equal(["a", "b"], JsonFieldValueReader.ToStrings(new JsonArray("a", "b")));
        Assert.Empty(JsonFieldValueReader.ToStrings(null));
    }

    [Fact]
    public async Task TheFieldStates_SayWhichFieldsAreHidden_AndWhichAreEmpty()
    {
        // Arrange: no outage, so "Who else is affected?" doesn't apply; nobody gave the model.
        var kit = new Kit();
        var item = Kit.NewItem();
        Post(item, outage: false, affected: null);

        // Act
        var states = await ((IContentFieldStateService)kit.Service).GetStatesAsync(item, kit.Type);

        // Assert: a page that lists the item's fields shows "Not set" only for Model.
        Assert.Equal(
            [("Request.Outage", "Outage", false, false), ("Request.Affected", "Affected", true, true), ("Request.Model", "Model", false, true)],
            states.Select(state => (state.Key, state.DisplayName, state.IsHidden, state.IsEmpty)));
    }

    private static void Post(ContentItem item, bool outage, string affected)
    {
        var part = (JsonObject)((JsonObject)item.Content)["Request"];
        part["Outage"] = new JsonObject { ["Value"] = outage };
        part["Affected"] = new JsonObject { ["Text"] = affected };
    }

    private static ContentItem Taxonomy()
    {
        var item = new ContentItem { ContentItemId = "tax", ContentType = "Taxonomy" };
        ((JsonObject)item.Content)["TaxonomyPart"] = new JsonObject
        {
            ["Terms"] = new JsonArray(
                new JsonObject
                {
                    ["ContentItemId"] = "hardware",
                    ["DisplayText"] = "Hardware",
                    ["Terms"] = new JsonArray(
                        new JsonObject { ["ContentItemId"] = "laptop", ["DisplayText"] = "Laptop" },
                        new JsonObject { ["ContentItemId"] = "printer", ["DisplayText"] = "Printer" }),
                }),
        };

        return item;
    }

    private sealed class Kit
    {
        public Kit(params IContentFieldVisibilityProvider[] providers)
        {
            var type = new ContentTypeDefinition("Request", "Request",
            [
                new ContentTypePartDefinition("Request", new ContentPartDefinition("Request",
                [
                    new ContentPartFieldDefinition(new ContentFieldDefinition("BooleanField"), "Outage", []),
                    new ContentPartFieldDefinition(new ContentFieldDefinition("TextField"), "Affected", new JsonObject
                    {
                        [nameof(ConditionalFieldSettings)] = JsonSerializer.SerializeToNode(new ConditionalFieldSettings { ShowWhen = _whenOutage }),
                    }),
                    new ContentPartFieldDefinition(new ContentFieldDefinition("TextField"), "Model", []),
                ], []), []),
            ], []);

            var definitions = new Mock<IContentDefinitionManager>();
            definitions.Setup(manager => manager.GetTypeDefinitionAsync("Request")).ReturnsAsync(type);

            Type = type;
            Service = new ConditionalFieldsService(
                [new JsonFieldValueReader("BooleanField", "Value"), new JsonFieldValueReader("TextField", "Text")],
                [],
                providers);
            Handler = new ConditionalFieldsDisplayHandler(definitions.Object, Service, Snapshots);
        }

        public ConditionalFieldsService Service { get; }

        public ContentTypeDefinition Type { get; }

        public ConditionalFieldsSnapshots Snapshots { get; } = new();

        public ConditionalFieldsDisplayHandler Handler { get; }

        public static ContentItem NewItem()
        {
            var item = new ContentItem { ContentType = "Request" };
            ((JsonObject)item.Content)["Request"] = new JsonObject();

            return item;
        }

        public static UpdateEditorContext Context(string prefix = "")
            => new(null, string.Empty, false, prefix, null, null, new FakeUpdateModel(new object()));
    }
}
