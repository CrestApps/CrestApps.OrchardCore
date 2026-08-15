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
}
