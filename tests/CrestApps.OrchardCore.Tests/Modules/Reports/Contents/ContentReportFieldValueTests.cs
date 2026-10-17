using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Reports.Contents;
using CrestApps.OrchardCore.Reports.Contents.Models;
using CrestApps.OrchardCore.Reports.Contents.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Contents;

public sealed class ContentReportFieldValueTests
{
    private const string FullCustomerJson =
        """
        {
          "Customer": {
            "Email": { "Text": "ada@example.com" },
            "Balance": { "Value": 1250.75 },
            "Vip": { "Value": true },
            "Birthday": { "Value": "1990-05-17T00:00:00" },
            "LastContact": { "Value": "2026-03-01T14:30:00Z" },
            "PreferredTime": { "Value": "09:30:00" },
            "Notes": { "Html": "<p>Hello</p>" },
            "Bio": { "Markdown": "**Hi**" },
            "Website": { "Url": "https://example.com", "Text": "Site", "Target": "_blank" },
            "Tags": { "Values": [ "gold", "", "early" ] },
            "Photos": { "Paths": [ "customers/ada.jpg", "customers/ada-2.jpg" ], "MediaTexts": [ "", "" ] },
            "Categories": { "TaxonomyContentItemId": "taxonomy-1", "TermContentItemIds": [ "term-1", "term-2" ] },
            "AccountManagers": { "UserIds": [ "user-1", "user-2" ], "UserNames": [ "alice", "bob" ] },
            "Phone": { "PhoneNumber": "+17025550100", "CountryCode": "US", "NationalNumber": "7025550100" },
            "Rating": { "Value": 4 },
            "Nickname": { "Text": "Ace" }
          },
          "TitlePart": { "Title": "Ada Lovelace" },
          "AutoroutePart": { "Path": "customers/ada" }
        }
        """;

    [Fact]
    public void GetValue_ReadsEveryBuiltInField_AsItsDeclaredClrType()
    {
        // Arrange
        using var services = ContentReportTestHelpers.Services();
        var builder = services.GetRequiredService<ContentReportSchemaBuilder>();
        var contentItem = ContentReportTestHelpers.Item("Customer", "customer-1", FullCustomerJson, displayText: "Ada Lovelace");

        // Act
        var values = ContentReportTestHelpers.ReadValues(builder, ContentReportTestHelpers.CustomerType(), contentItem);

        // Assert
        Assert.Equal("customer-1", values["ContentItemId"]);
        Assert.Equal("customer-1-v1", values["ContentItemVersionId"]);
        Assert.Equal("Ada Lovelace", values["DisplayText"]);
        Assert.Equal("Customer", values["ContentType"]);
        Assert.Equal("owner-1", values["Owner"]);
        Assert.Equal("admin", values["Author"]);
        Assert.Equal(new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc), values["CreatedUtc"]);
        Assert.Equal(DateTimeKind.Utc, ((DateTime)values["CreatedUtc"]).Kind);
        Assert.Equal(true, values["Published"]);
        Assert.Equal("ada@example.com", values["Customer.Email"]);
        Assert.Equal(1250.75m, values["Customer.Balance"]);
        Assert.Equal(true, values["Customer.Vip"]);
        Assert.Equal(new DateTime(1990, 5, 17), values["Customer.Birthday"]);
        Assert.Equal(new DateTime(2026, 3, 1, 14, 30, 0, DateTimeKind.Utc), values["Customer.LastContact"]);
        Assert.Equal(DateTimeKind.Utc, ((DateTime)values["Customer.LastContact"]).Kind);
        Assert.Equal("09:30:00", values["Customer.PreferredTime"]);
        Assert.Equal("<p>Hello</p>", values["Customer.Notes"]);
        Assert.Equal("**Hi**", values["Customer.Bio"]);
        Assert.Equal("https://example.com", values["Customer.Website"]);
        Assert.Equal("Site", values["Customer.Website.Text"]);
        Assert.Equal("gold,early", values["Customer.Tags"]);
        Assert.Equal("customers/ada.jpg,customers/ada-2.jpg", values["Customer.Photos"]);
        Assert.Equal("term-1,term-2", values["Customer.Categories"]);
        Assert.Equal("user-1", values["Customer.AccountManagers"]);
        Assert.Equal("user-1,user-2", values["Customer.AccountManagers.UserIds"]);
        Assert.Equal("alice,bob", values["Customer.AccountManagers.UserNames"]);
        Assert.Equal("+17025550100", values["Customer.Phone"]);
        Assert.Equal("Ada Lovelace", values["TitlePart.Title"]);
        Assert.Equal("customers/ada", values["AutoroutePart.Path"]);
    }

    [Fact]
    public void GetValue_WhenFieldTypeIsUnknown_ReadsTextOrValueAsText()
    {
        // Arrange
        using var services = ContentReportTestHelpers.Services();
        var builder = services.GetRequiredService<ContentReportSchemaBuilder>();
        var contentItem = ContentReportTestHelpers.Item("Customer", "customer-1", FullCustomerJson);

        // Act
        var values = ContentReportTestHelpers.ReadValues(builder, ContentReportTestHelpers.CustomerType(), contentItem);

        // Assert
        Assert.Equal("4", values["Customer.Rating"]);
        Assert.Equal("Ace", values["Customer.Nickname"]);
    }

    [Fact]
    public void GetValue_WhenValuesAreMissingOrNull_ReturnsNull()
    {
        // Arrange
        using var services = ContentReportTestHelpers.Services();
        var builder = services.GetRequiredService<ContentReportSchemaBuilder>();
        var contentItem = ContentReportTestHelpers.Item(
            "Customer",
            "customer-2",
            """
            {
              "Customer": {
                "Balance": { "Value": null },
                "Vip": { },
                "Birthday": { "Value": "" },
                "Website": { "Url": "https://example.com" },
                "Tags": { "Values": [ ] },
                "AccountManagers": { "UserIds": [ ] },
                "Rating": { "Other": 4 }
              }
            }
            """);

        // Act
        var values = ContentReportTestHelpers.ReadValues(builder, ContentReportTestHelpers.CustomerType(), contentItem);

        // Assert
        Assert.Null(values["Customer.Email"]);
        Assert.Null(values["Customer.Balance"]);
        Assert.Null(values["Customer.Vip"]);
        Assert.Null(values["Customer.Birthday"]);
        Assert.Equal("https://example.com", values["Customer.Website"]);
        Assert.Null(values["Customer.Website.Text"]);
        Assert.Null(values["Customer.Tags"]);
        Assert.Null(values["Customer.AccountManagers"]);
        Assert.Null(values["Customer.AccountManagers.UserIds"]);
        Assert.Null(values["Customer.AccountManagers.UserNames"]);
        Assert.Null(values["Customer.Rating"]);
        Assert.Null(values["TitlePart.Title"]);
    }

    [Fact]
    public void GetValue_ReadsContentPickerIds_FirstAndJoined()
    {
        // Arrange
        using var services = ContentReportTestHelpers.Services();
        var builder = services.GetRequiredService<ContentReportSchemaBuilder>();
        var contentItem = ContentReportTestHelpers.Item(
            "Order",
            "order-1",
            """
            {
              "Order": {
                "Customer": { "ContentItemIds": [ "customer-1", "customer-2" ] },
                "Total": { "Value": 99 }
              }
            }
            """);

        // Act
        var values = ContentReportTestHelpers.ReadValues(builder, ContentReportTestHelpers.OrderType(), contentItem);

        // Assert
        Assert.Equal("customer-1", values["Order.Customer"]);
        Assert.Equal("customer-1,customer-2", values["Order.Customer.ContentItemIds"]);
        Assert.Null(values["Order.Customer.DisplayText"]);
        Assert.Equal(99m, values["Order.Total"]);
    }

    [Fact]
    public void GetValue_ReadsCommonPartData_AndMarksContainerAndLocalizationSetAsIdentifiers()
    {
        // Arrange
        var definition = ContentReportTestHelpers.Type(
            "Article",
            "Article",
            stereotype: null,
            ContentReportTestHelpers.Part("AliasPart", "AliasPart", "Alias"),
            ContentReportTestHelpers.Part("HtmlBodyPart", "HtmlBodyPart", "Body"),
            ContentReportTestHelpers.Part("MarkdownBodyPart", "MarkdownBodyPart", "Markdown"),
            ContentReportTestHelpers.Part("ContainedPart", "ContainedPart", "Contained"),
            ContentReportTestHelpers.Part("LocalizationPart", "LocalizationPart", "Localization"));
        var contentItem = ContentReportTestHelpers.Item(
            "Article",
            "article-1",
            """
            {
              "AliasPart": { "Alias": "welcome" },
              "HtmlBodyPart": { "Html": "<p>Body</p>" },
              "MarkdownBodyPart": { "Markdown": "# Body" },
              "ContainedPart": { "ListContentItemId": "blog-1", "Order": 3 },
              "LocalizationPart": { "Culture": "fr-CA", "LocalizationSet": "set-1" }
            }
            """);
        using var services = ContentReportTestHelpers.Services();
        var builder = services.GetRequiredService<ContentReportSchemaBuilder>();

        // Act
        var descriptors = builder.Build(definition).Skip(10).Select(field => field.Descriptor).ToList();
        var values = ContentReportTestHelpers.ReadValues(builder, definition, contentItem);

        // Assert
        Assert.Equal(
            [
                ("AliasPart.Alias", false),
                ("HtmlBodyPart.Html", false),
                ("MarkdownBodyPart.Markdown", false),
                ("ContainedPart.ListContentItemId", true),
                ("LocalizationPart.Culture", false),
                ("LocalizationPart.LocalizationSet", true),
            ],
            descriptors.Select(descriptor => (descriptor.Name, descriptor.IsIdentifier)));
        Assert.Equal("welcome", values["AliasPart.Alias"]);
        Assert.Equal("<p>Body</p>", values["HtmlBodyPart.Html"]);
        Assert.Equal("# Body", values["MarkdownBodyPart.Markdown"]);
        Assert.Equal("blog-1", values["ContainedPart.ListContentItemId"]);
        Assert.Equal("fr-CA", values["LocalizationPart.Culture"]);
        Assert.Equal("set-1", values["LocalizationPart.LocalizationSet"]);
    }

    [Fact]
    public void ReadProperty_WhenTheValueWasNotParsedFromJson_StillReadsIt()
    {
        // Arrange
        var element = new JsonObject
        {
            ["Value"] = JsonValue.Create(12.5m),
            ["Values"] = new JsonArray(JsonValue.Create("a"), JsonValue.Create(7)),
        };

        // Act
        var single = ReportDataValues.Coerce(ContentReportJson.ReadProperty(element, ContentReportValueMode.Single, "Value"), ReportDataType.Decimal);
        var joined = ContentReportJson.ReadProperty(element, ContentReportValueMode.Join, "Values");

        // Assert
        Assert.Equal(12.5m, single);
        Assert.Equal("a,7", joined);
    }

    [Theory]
    [InlineData("2026-03-01T14:30:00")]
    [InlineData("2026-03-01T14:30:00Z")]
    public void ToReportValue_ReturnsDateTimesInUtc(string raw)
    {
        // Act
        var value = ContentsReportDataSource.ToReportValue(raw, ReportDataType.DateTime);

        // Assert
        var date = Assert.IsType<DateTime>(value);
        Assert.Equal(DateTimeKind.Utc, date.Kind);
        Assert.Equal(new DateTime(2026, 3, 1, 14, 30, 0, DateTimeKind.Utc), date);
    }

    [Fact]
    public void ToReportValue_ReturnsDatesWithoutTimeOrZone()
    {
        // Act
        var value = ContentsReportDataSource.ToReportValue("2026-03-01T00:00:00Z", ReportDataType.Date);

        // Assert
        var date = Assert.IsType<DateTime>(value);
        Assert.Equal(DateTimeKind.Unspecified, date.Kind);
        Assert.Equal(new DateTime(2026, 3, 1), date);
    }
}
