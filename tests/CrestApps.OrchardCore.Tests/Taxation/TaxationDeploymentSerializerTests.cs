using CrestApps.OrchardCore.Taxation.Deployments;
using CrestApps.OrchardCore.Taxation.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

public sealed class TaxationDeploymentSerializerTests
{
    [Fact]
    public void Export_OmitsEnvironmentOwnedMembers_ButKeepsIdentityAndConfiguration()
    {
        var category = new TaxCategory
        {
            ItemId = "cat-1",
            Name = "Electronics",
            Code = "ELEC",
            ParentCode = "GOODS",
            Description = "Electronic goods",
            CreatedUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ModifiedUtc = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Author = "admin",
            OwnerId = "owner-1",
        };

        var node = TaxationDeploymentSerializer.Export(category);

        Assert.Equal("cat-1", (string)node["ItemId"]);
        Assert.Equal("Electronics", (string)node["Name"]);
        Assert.Equal("ELEC", (string)node["Code"]);
        Assert.Equal("GOODS", (string)node["ParentCode"]);

        Assert.False(node.ContainsKey("CreatedUtc"));
        Assert.False(node.ContainsKey("ModifiedUtc"));
        Assert.False(node.ContainsKey("Author"));
        Assert.False(node.ContainsKey("OwnerId"));
    }

    [Fact]
    public void Populate_AppliesConfiguration_ButNeverIdentityOrEnvironmentOwnedMembers()
    {
        var source = new TaxCategory
        {
            ItemId = "cat-1",
            Name = "Electronics",
            Code = "ELEC",
            CreatedUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ModifiedUtc = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Author = "admin",
            OwnerId = "owner-1",
        };

        var data = TaxationDeploymentSerializer.Export(source);

        // Simulate an existing target entry that keeps its own identity and audit stamps.
        var target = new TaxCategory
        {
            ItemId = "existing-id",
            Author = "existing-author",
            OwnerId = "existing-owner",
            CreatedUtc = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        TaxationDeploymentSerializer.Populate(target, data);

        Assert.Equal("Electronics", target.Name);
        Assert.Equal("ELEC", target.Code);

        // Identity and environment-owned members are preserved on the target.
        Assert.Equal("existing-id", target.ItemId);
        Assert.Equal("existing-author", target.Author);
        Assert.Equal("existing-owner", target.OwnerId);
        Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), target.CreatedUtc);
    }

    [Fact]
    public void Populate_ClearsValuesThatWereClearedInTheSource()
    {
        var source = new TaxCategory
        {
            Name = "Electronics",
            Code = "ELEC",
            ParentCode = null,
        };

        var data = TaxationDeploymentSerializer.Export(source);

        var target = new TaxCategory
        {
            Name = "Old",
            Code = "OLD",
            ParentCode = "STALE",
        };

        TaxationDeploymentSerializer.Populate(target, data);

        Assert.Equal("Electronics", target.Name);
        Assert.Null(target.ParentCode);
    }

    [Fact]
    public void TaxTable_RoundTrips_RowsAndEffectiveDates()
    {
        var source = new TaxTable
        {
            ItemId = "tbl-1",
            Name = "Luxury brackets",
            EffectiveFromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveToUtc = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Rows =
            [
                new TaxTableRow { Minimum = 0m, Maximum = 100m, Rate = 0.05m },
                new TaxTableRow { Minimum = 100m, Maximum = null, Rate = 0.07m, FixedAmount = 2m },
            ],
        };

        var data = TaxationDeploymentSerializer.Export(source);

        var target = new TaxTable { ItemId = "existing-id" };
        TaxationDeploymentSerializer.Populate(target, data);

        Assert.Equal("Luxury brackets", target.Name);
        Assert.Equal(source.EffectiveFromUtc, target.EffectiveFromUtc);
        Assert.Equal(source.EffectiveToUtc, target.EffectiveToUtc);
        Assert.Equal(2, target.Rows.Count);
        Assert.Equal(0m, target.Rows[0].Minimum);
        Assert.Equal(100m, target.Rows[0].Maximum);
        Assert.Equal(0.05m, target.Rows[0].Rate);
        Assert.Null(target.Rows[1].Maximum);
        Assert.Equal(2m, target.Rows[1].FixedAmount);
        Assert.Equal("existing-id", target.ItemId);
    }

    [Fact]
    public void TaxTable_Version_IsEnvironmentOwned_AndNeverImported()
    {
        var source = new TaxTable
        {
            ItemId = "tbl-1",
            Name = "Rates",
            Version = 9,
        };

        var data = TaxationDeploymentSerializer.Export(source);

        Assert.False(data.ContainsKey("Version"));

        var target = new TaxTable { ItemId = "existing-id", Version = 3 };
        TaxationDeploymentSerializer.Populate(target, data);

        // A recipe can never regress or reuse the authoritative version stamp.
        Assert.Equal(3, target.Version);
    }
}
