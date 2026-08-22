using CrestApps.OrchardCore.Taxation.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

public sealed class TaxTableTests
{
    [Fact]
    public void Clone_CopiesScalarFields()
    {
        // Arrange
        var table = new TaxTable
        {
            ItemId = "table-1",
            Name = "US Sales Tax",
            Version = 3,
            EffectiveFromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveToUtc = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            CreatedUtc = new DateTime(2023, 12, 1, 0, 0, 0, DateTimeKind.Utc),
            ModifiedUtc = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            Author = "admin",
            OwnerId = "owner-1",
        };

        // Act
        var clone = table.Clone();

        // Assert
        Assert.NotSame(table, clone);
        Assert.Equal(table.ItemId, clone.ItemId);
        Assert.Equal(table.Name, clone.Name);
        Assert.Equal(table.Version, clone.Version);
        Assert.Equal(table.EffectiveFromUtc, clone.EffectiveFromUtc);
        Assert.Equal(table.EffectiveToUtc, clone.EffectiveToUtc);
        Assert.Equal(table.CreatedUtc, clone.CreatedUtc);
        Assert.Equal(table.ModifiedUtc, clone.ModifiedUtc);
        Assert.Equal(table.Author, clone.Author);
        Assert.Equal(table.OwnerId, clone.OwnerId);
    }

    [Fact]
    public void Clone_DeepCopiesRows()
    {
        // Arrange
        var table = new TaxTable
        {
            Name = "Brackets",
            Rows =
            [
                new TaxTableRow { Minimum = 0m, Maximum = 100m, Rate = 0.05m, FixedAmount = 1m, BaseAmount = 2m },
            ],
        };

        // Act
        var clone = table.Clone();
        clone.Rows[0].Rate = 0.09m;
        clone.Rows.Add(new TaxTableRow { Minimum = 100m });

        // Assert
        Assert.NotSame(table.Rows, clone.Rows);
        Assert.Single(table.Rows);
        Assert.Equal(0.05m, table.Rows[0].Rate);
    }

    [Fact]
    public void TaxTableRow_Clone_CopiesAllFields()
    {
        // Arrange
        var row = new TaxTableRow
        {
            Minimum = 10m,
            Maximum = 250m,
            Rate = 0.075m,
            FixedAmount = 3m,
            BaseAmount = 12m,
        };

        // Act
        var clone = row.Clone();

        // Assert
        Assert.NotSame(row, clone);
        Assert.Equal(row.Minimum, clone.Minimum);
        Assert.Equal(row.Maximum, clone.Maximum);
        Assert.Equal(row.Rate, clone.Rate);
        Assert.Equal(row.FixedAmount, clone.FixedAmount);
        Assert.Equal(row.BaseAmount, clone.BaseAmount);
    }
}
