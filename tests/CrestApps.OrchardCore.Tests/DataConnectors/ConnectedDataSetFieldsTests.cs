using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

public sealed class ConnectedDataSetFieldsTests
{
    [Theory]
    [InlineData("address.city", "address_city")]
    [InlineData("First Name", "First_Name")]
    [InlineData("2024 total", "F_2024_total")]
    [InlineData("_id", "id")]
    [InlineData("", "Field")]
    public void ToFieldName_MakesANameConsumersCanUse(string sourceName, string expected)
    {
        Assert.Equal(expected, ConnectedDataSetFields.ToFieldName(sourceName));
    }

    [Fact]
    public void Create_GivesEveryFieldAUniqueName_AndKeepsTheSourceNameAndType()
    {
        // Arrange
        DataField[] source =
        [
            new("a.b", "a.b", DataFieldType.Integer) { IsIdentifier = true },
            new("a_b", "a_b", DataFieldType.Text),
        ];

        // Act
        var fields = ConnectedDataSetFields.Create(source);

        // Assert
        Assert.Equal(["a_b", "a_b_2"], fields.Select(field => field.Name));
        Assert.Equal(["a.b", "a_b"], fields.Select(field => field.SourceName));
        Assert.True(fields[0].IsIdentifier);
        Assert.Equal(DataFieldType.Integer, fields[0].SourceType);
        Assert.All(fields, field => Assert.True(field.IsIncluded));
    }

    [Fact]
    public void Compare_ReportsAddedRemovedAndChangedSourceFields()
    {
        // Arrange
        var fields = DataConnectorTestData.DataSet().Fields;
        DataField[] source =
        [
            new("id", "id", DataFieldType.Text),
            new("name", "name", DataFieldType.Text),
            new("email", "email", DataFieldType.Text),
        ];

        // Act
        var changes = ConnectedDataSetFields.Compare(fields, source);

        // Assert
        Assert.True(changes.HasChanges);
        Assert.Equal(["email"], changes.Added);
        Assert.Equal(["modified"], changes.Removed);
        Assert.Single(changes.Changed);
        Assert.Contains("Added: email", changes.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Merge_AddsNewFields_KeepsRemovedOnes_AndKeepsATypeTheAdministratorChose()
    {
        // Arrange
        var fields = DataConnectorTestData.DataSet().Fields;
        fields[1].Type = DataFieldType.Integer;

        DataField[] source =
        [
            new("id", "id", DataFieldType.Decimal),
            new("name", "name", DataFieldType.Boolean),
            new("email", "email", DataFieldType.Text),
        ];

        // Act
        ConnectedDataSetFields.Merge(fields, source);

        // Assert
        Assert.Equal(4, fields.Count);
        Assert.Contains(fields, field => field.SourceName == "modified");
        Assert.Contains(fields, field => field.SourceName == "email" && field.IsIncluded);

        // The identifier followed the source, so it follows the source again.
        Assert.Equal(DataFieldType.Decimal, fields[0].Type);

        // The administrator read name as a number, which stays.
        Assert.Equal(DataFieldType.Integer, fields[1].Type);
        Assert.Equal(DataFieldType.Boolean, fields[1].SourceType);
    }

    [Fact]
    public void ToDataFields_ListsOnlyIncludedFields_AndMarksASingleIdentifierKeyFilterable()
    {
        // Arrange
        var dataSet = DataConnectorTestData.DataSet();
        dataSet.Fields[1].IsIncluded = false;

        // Act
        var fields = ConnectedDataSetFields.ToDataFields(dataSet, identifiersAreKeyFilterable: true);

        // Assert
        Assert.Equal(["Id", "Modified"], fields.Select(field => field.Name));
        Assert.True(fields[0].IsKeyFilterable);
    }

    [Fact]
    public void ToDescriptor_KeepsTheDefaultDateFieldOnlyWhenItIsAnIncludedDate()
    {
        // Arrange
        var dataSet = DataConnectorTestData.DataSet();
        dataSet.DefaultDateField = "Name";

        // Act
        var notADate = ConnectedDataSetFields.ToDescriptor(dataSet, "Group");
        dataSet.DefaultDateField = "Modified";
        var date = ConnectedDataSetFields.ToDescriptor(dataSet, "Group");

        // Assert
        Assert.Null(notADate.DefaultDateField);
        Assert.Equal("Modified", date.DefaultDateField);
        Assert.Equal("Group", date.Group);
    }

    [Fact]
    public void Compare_WithFieldsOfAnAdministrator_DoesNotReportChangesWhenTheSourceIsTheSame()
    {
        // Arrange
        var fields = new List<ConnectedDataField>
        {
            new() { Name = "Renamed", SourceName = "id", Type = DataFieldType.Text, SourceType = DataFieldType.Integer },
        };

        // Act
        var changes = ConnectedDataSetFields.Compare(fields, [new DataField("id", "id", DataFieldType.Integer)]);

        // Assert
        Assert.False(changes.HasChanges);
    }
}
