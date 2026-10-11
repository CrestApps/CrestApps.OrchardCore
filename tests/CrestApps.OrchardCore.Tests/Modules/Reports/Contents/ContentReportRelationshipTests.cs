using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Contents.ContentReportTestHelpers;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Contents;

/// <summary>
/// Relationships between content types are read from their definitions, never hard-coded: a content picker
/// references the types it accepts, a type that a ListPart contains references its list, and owners and user pickers
/// reference the users data set.
/// </summary>
public sealed class ContentReportRelationshipTests
{
    [Fact]
    public async Task ContentPicker_ReferencesTheTypesItAccepts()
    {
        // Arrange
        var order = Type("Order", "Order", null, Part("Order", "Order", "Order", Picker("Customer", "Customer", "Lead")));
        var source = Source(Definitions(order, CustomerType()), "Order", "Customer");

        // Act
        var schema = await source.GetSchemaAsync("Order", Context(), TestContext.Current.CancellationToken);

        // Assert
        var customer = schema.FindField("Order.Customer");
        Assert.Equal(["Customer", "Lead"], customer.References.Select(reference => reference.DataSet));
        Assert.All(customer.References, reference =>
        {
            Assert.Equal(ReportsConstants.ContentsDataSource, reference.Source);
            Assert.Equal("ContentItemId", reference.Field);
        });
    }

    [Fact]
    public async Task ContainedItem_ReferencesTheListThatContainsIt()
    {
        // Arrange
        var account = Type("Account", "Account", null, ListPart("Contact"));
        var contact = Type("Contact", "Contact", null, Part("TitlePart", "TitlePart", "Title"));
        var source = Source(Definitions(account, contact), "Account", "Contact");

        // Act
        var schema = await source.GetSchemaAsync("Contact", Context(), TestContext.Current.CancellationToken);
        var dataSets = await source.GetDataSetsAsync(Context(), TestContext.Current.CancellationToken);

        // Assert
        var container = schema.FindField("ContainedPart.ListContentItemId");
        Assert.NotNull(container);
        Assert.True(container.IsIdentifier);
        Assert.Equal("Account", Assert.Single(container.References).DataSet);
        Assert.Contains(dataSets.Single(dataSet => dataSet.Name == "Contact").References, reference => reference.DataSet == "Account");
    }

    [Fact]
    public async Task Owner_ReferencesTheUsersDataSet()
    {
        // Arrange
        var source = Source(Definitions(CustomerType()), "Customer");

        // Act
        var schema = await source.GetSchemaAsync("Customer", Context(), TestContext.Current.CancellationToken);

        // Assert
        var owner = Assert.Single(schema.FindField("Owner").References);
        Assert.Equal(ReportsConstants.UsersDataSource, owner.Source);
        Assert.Equal(ReportsConstants.UsersDataSet, owner.DataSet);
        Assert.Equal(ReportsConstants.UserIdField, owner.Field);
        Assert.Equal(ReportsConstants.UsersDataSet, Assert.Single(schema.FindField("Customer.AccountManagers").References).DataSet);
    }

    [Fact]
    public async Task Widgets_AreNotOfferedAsDataSets()
    {
        // Arrange
        var banner = Type("Banner", "Banner", "Widget", Part("TitlePart", "TitlePart", "Title"));
        var source = Source(Definitions(banner, CustomerType()), "Banner", "Customer");

        // Act
        var dataSets = await source.GetDataSetsAsync(Context(), TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync("Banner", Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Customer"], dataSets.Select(dataSet => dataSet.Name));
        Assert.Null(schema);
    }

    private static IReportDataSource Source(Mock<IContentDefinitionManager> definitions, params string[] viewable)
    {
        return Services(definitions.Object, AuthorizationFor(viewable).Object)
            .GetServices<IReportDataSource>()
            .Single(source => source.Name == ReportsConstants.ContentsDataSource);
    }

    private static ReportDataSourceContext Context()
    {
        return new ReportDataSourceContext
        {
            User = User,
        };
    }

    private static ContentPartFieldDefinition Picker(string name, params string[] contentTypes)
    {
        var settings = new JsonObject
        {
            ["ContentPartFieldSettings"] = new JsonObject { ["DisplayName"] = name },
            ["ContentPickerFieldSettings"] = new JsonObject { ["DisplayedContentTypes"] = new JsonArray(contentTypes.Select(type => (JsonNode)type).ToArray()) },
        };

        return new ContentPartFieldDefinition(new ContentFieldDefinition("ContentPickerField"), name, settings);
    }

    private static ContentTypePartDefinition ListPart(params string[] containedTypes)
    {
        var settings = new JsonObject
        {
            ["ContentTypePartSettings"] = new JsonObject { ["DisplayName"] = "List" },
            ["ListPartSettings"] = new JsonObject { ["ContainedContentTypes"] = new JsonArray(containedTypes.Select(type => (JsonNode)type).ToArray()) },
        };

        return new ContentTypePartDefinition("ListPart", new ContentPartDefinition("ListPart", [], []), settings);
    }
}
