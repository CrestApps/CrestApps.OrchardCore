using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.Tests.Regression;

/// <summary>
/// Audit defect 10: the Contact Center and Omnichannel copies of the catalog deployment serializer did not exclude the
/// <c>Version</c> stamp, so a deployment plan carried it and replaying the plan regressed it. Both modules now use
/// <see cref="CatalogDeploymentSerializer"/>.
/// </summary>
public sealed class CatalogDeploymentSerializerVersionTests
{
    [Fact]
    public void Export_LeavesOutTheVersionStamp()
    {
        // Arrange
        var entry = new VersionedEntry
        {
            ItemId = "entry-1",
            Name = "Sales",
            Version = 7,
        };

        // Act
        var json = CatalogDeploymentSerializer.Export(entry);

        // Assert
        Assert.Equal("Sales", (string)json["Name"]);
        Assert.False(json.ContainsKey("Version"));
    }

    [Fact]
    public void Populate_KeepsTheTargetVersionStamp_WhenThePlanCarriesOne()
    {
        // Arrange
        var target = new VersionedEntry
        {
            ItemId = "entry-1",
            Name = "Old name",
            Version = 12,
        };

        var plan = new JsonObject
        {
            ["Name"] = "New name",
            ["Version"] = 3,
        };

        // Act
        CatalogDeploymentSerializer.Populate(target, plan);

        // Assert
        Assert.Equal("New name", target.Name);
        Assert.Equal(12, target.Version);
    }

    [Theory]
    [InlineData("CrestApps.OrchardCore.ContactCenter.Deployments.ContactCenterDeploymentSerializer, CrestApps.OrchardCore.ContactCenter")]
    [InlineData("CrestApps.OrchardCore.Omnichannel.Managements.Deployments.OmnichannelDeploymentSerializer, CrestApps.OrchardCore.Omnichannel.Managements")]
    public void TheCopiedSerializers_AreGone(string typeName)
    {
        // Act
        var type = Type.GetType(typeName, throwOnError: false);

        // Assert
        Assert.Null(type);
    }

    private sealed class VersionedEntry
    {
        public string ItemId { get; set; }

        public string Name { get; set; }

        public long Version { get; set; }
    }
}
