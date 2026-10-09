using CrestApps.OrchardCore.Reports.Contents;
using CrestApps.OrchardCore.Reports.Contents.Models;
using CrestApps.OrchardCore.Reports.Contents.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Contents;

public sealed class ContentReportSchemaBuilderTests
{
    [Fact]
    public void Build_ListsMetadataFirst_WithStableNamesAndTypes()
    {
        // Arrange
        using var services = ContentReportTestHelpers.Services();
        var builder = services.GetRequiredService<ContentReportSchemaBuilder>();

        // Act
        var fields = builder.Build(ContentReportTestHelpers.CustomerType()).Select(field => field.Descriptor).ToList();

        // Assert
        Assert.Equal(
            [
                ("ContentItemId", ReportDataType.Text, true),
                ("ContentItemVersionId", ReportDataType.Text, false),
                ("DisplayText", ReportDataType.Text, false),
                ("ContentType", ReportDataType.Text, false),
                ("Owner", ReportDataType.Text, true),
                ("Author", ReportDataType.Text, false),
                ("CreatedUtc", ReportDataType.DateTime, false),
                ("ModifiedUtc", ReportDataType.DateTime, false),
                ("PublishedUtc", ReportDataType.DateTime, false),
                ("Published", ReportDataType.Boolean, false),
            ],
            fields.Take(10).Select(field => (field.Name, field.DataType, field.IsIdentifier)));
        Assert.All(fields.Take(10), field => Assert.Equal("Content item", field.Group));
    }

    [Fact]
    public void Build_NamesPartFieldsByPartAndField_GroupedByPartDisplayName()
    {
        // Arrange
        using var services = ContentReportTestHelpers.Services();
        var builder = services.GetRequiredService<ContentReportSchemaBuilder>();

        // Act
        var fields = builder.Build(ContentReportTestHelpers.CustomerType())
            .Skip(10)
            .Select(field => field.Descriptor)
            .ToList();

        // Assert
        Assert.Equal(
            [
                ("Customer.Email", ReportDataType.Text, "Customer details", false),
                ("Customer.Balance", ReportDataType.Decimal, "Customer details", false),
                ("Customer.Vip", ReportDataType.Boolean, "Customer details", false),
                ("Customer.Birthday", ReportDataType.Date, "Customer details", false),
                ("Customer.LastContact", ReportDataType.DateTime, "Customer details", false),
                ("Customer.PreferredTime", ReportDataType.Text, "Customer details", false),
                ("Customer.Notes", ReportDataType.Text, "Customer details", false),
                ("Customer.Bio", ReportDataType.Text, "Customer details", false),
                ("Customer.Website", ReportDataType.Text, "Customer details", false),
                ("Customer.Website.Text", ReportDataType.Text, "Customer details", false),
                ("Customer.Tags", ReportDataType.Text, "Customer details", false),
                ("Customer.Photos", ReportDataType.Text, "Customer details", false),
                ("Customer.Categories", ReportDataType.Text, "Customer details", false),
                ("Customer.AccountManagers", ReportDataType.Text, "Customer details", true),
                ("Customer.AccountManagers.UserIds", ReportDataType.Text, "Customer details", false),
                ("Customer.AccountManagers.UserNames", ReportDataType.Text, "Customer details", false),
                ("Customer.Phone", ReportDataType.Text, "Customer details", false),
                ("Customer.Rating", ReportDataType.Text, "Customer details", false),
                ("Customer.Nickname", ReportDataType.Text, "Customer details", false),
                ("TitlePart.Title", ReportDataType.Text, "Title", false),
                ("AutoroutePart.Path", ReportDataType.Text, "Autoroute", false),
            ],
            fields.Select(field => (field.Name, field.DataType, field.Group, field.IsIdentifier)));
        Assert.Equal("Email", fields[0].DisplayName);
        Assert.Equal("Website (link text)", fields[9].DisplayName);
    }

    [Fact]
    public void Build_DescribesContentPicker_WithJoinableFirstIdAndSuffixedValues()
    {
        // Arrange
        using var services = ContentReportTestHelpers.Services();
        var builder = services.GetRequiredService<ContentReportSchemaBuilder>();

        // Act
        var fields = builder.Build(ContentReportTestHelpers.OrderType())
            .Skip(10)
            .Select(field => field.Descriptor)
            .ToList();

        // Assert
        Assert.Equal(
            [
                ("Order.Customer", true, "Customer"),
                ("Order.Customer.ContentItemIds", false, "Customer (all IDs)"),
                ("Order.Customer.DisplayText", false, "Customer (display text)"),
                ("Order.Total", false, "Total"),
            ],
            fields.Select(field => (field.Name, field.IsIdentifier, field.DisplayName)));
    }

    [Fact]
    public void Build_WhenAProviderIsRegisteredLater_ItReplacesTheBuiltInProvider()
    {
        // Arrange
        using var services = ContentReportTestHelpers.Services(configure: collection =>
            collection.AddContentReportFieldProvider("TextField", ReportDataType.Integer, ContentReportValueMode.Single, "Length"));
        var builder = services.GetRequiredService<ContentReportSchemaBuilder>();

        // Act
        var email = builder.Build(ContentReportTestHelpers.CustomerType())
            .Single(field => field.Descriptor.Name == "Customer.Email");

        // Assert
        Assert.Equal(ReportDataType.Integer, email.Descriptor.DataType);
    }

    [Fact]
    public void Build_WhenTwoFieldsShareAName_KeepsTheFirst()
    {
        // Arrange
        var definition = ContentReportTestHelpers.Type(
            "Page",
            "Page",
            stereotype: null,
            ContentReportTestHelpers.Part(
                "TitlePart",
                "TitlePart",
                "Title",
                ContentReportTestHelpers.Field("Title", "NumericField", "Title number")));
        using var services = ContentReportTestHelpers.Services();
        var builder = services.GetRequiredService<ContentReportSchemaBuilder>();

        // Act
        var fields = builder.Build(definition).Where(field => field.Descriptor.Name == "TitlePart.Title").ToList();

        // Assert
        var field = Assert.Single(fields);
        Assert.Equal(ReportDataType.Text, field.Descriptor.DataType);
    }

    [Fact]
    public void GetFieldProvider_WhenNoProviderHandlesTheType_ReturnsTheGenericTextProvider()
    {
        // Arrange
        using var services = ContentReportTestHelpers.Services();
        var builder = services.GetRequiredService<ContentReportSchemaBuilder>();

        // Act
        var provider = builder.GetFieldProvider("UnknownField");

        // Assert
        Assert.Equal(ContentReportSchemaBuilder.FallbackFieldType, provider.FieldType);
    }
}
