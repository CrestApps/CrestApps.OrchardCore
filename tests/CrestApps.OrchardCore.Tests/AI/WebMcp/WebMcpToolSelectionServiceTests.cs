using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.AI.WebMcp.Services;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

public sealed class WebMcpToolSelectionServiceTests
{
    [Fact]
    public void Merge_ReplacesTheToolsTheUserMayAssignAndKeepsTheOthers()
    {
        // Arrange
        var current = new[] { "hiddenTool", "listAIProfiles", "docs-instance" };
        var toolsModel = Tools(("listAIProfiles", false), ("viewAIProfile", true));
        var instancesModel = Instances(("docs-instance", false), ("crm-instance", true));

        // Act
        var merged = WebMcpToolSelectionService.Merge(
            current,
            ["listAIProfiles", "viewAIProfile"],
            ["docs-instance", "crm-instance"],
            toolsModel,
            instancesModel);

        // Assert
        Assert.Equal(["hiddenTool", "viewAIProfile", "crm-instance"], merged);
    }

    [Fact]
    public void Merge_IgnoresPickedToolsTheUserMayNotAssign()
    {
        // Arrange
        var toolsModel = Tools(("listAIProfiles", true), ("deleteEverything", true));

        // Act
        var merged = WebMcpToolSelectionService.Merge([], ["listAIProfiles"], [], toolsModel, new EditToolInstancesViewModel());

        // Assert
        Assert.Equal(["listAIProfiles"], merged);
    }

    [Fact]
    public void Merge_HandlesAMissingSelectionAndPickers()
    {
        // Act
        var merged = WebMcpToolSelectionService.Merge(null, ["listAIProfiles"], [], null, null);

        // Assert
        Assert.Empty(merged);
    }

    [Fact]
    public void Merge_DoesNotRepeatATool()
    {
        // Arrange
        var toolsModel = Tools(("listAIProfiles", true));

        // Act
        var merged = WebMcpToolSelectionService.Merge(["LISTAIPROFILES"], ["listAIProfiles"], [], toolsModel, null);

        // Assert
        Assert.Equal(["listAIProfiles"], merged);
    }

    [Fact]
    public void PopulateInstances_MarksTheSelectedInstancesAndSortsByName()
    {
        // Arrange
        var model = new EditToolInstancesViewModel();
        var instances = new[]
        {
            new AIToolInstance { Name = "zeta" },
            new AIToolInstance { Name = "Alpha" },
        };

        // Act
        WebMcpToolSelectionService.PopulateInstances(model, instances, ["ALPHA"]);

        // Assert
        Assert.Equal(["Alpha", "zeta"], model.Instances.Select(instance => instance.Name));
        Assert.True(model.Instances[0].IsSelected);
        Assert.False(model.Instances[1].IsSelected);
    }

    private static EditProfileToolsViewModel Tools(params (string Name, bool Selected)[] tools) => new()
    {
        Tools = new Dictionary<string, ToolEntry[]>
        {
            ["Group"] = [.. tools.Select(tool => new ToolEntry { ItemId = tool.Name, IsSelected = tool.Selected })],
        },
    };

    private static EditToolInstancesViewModel Instances(params (string Name, bool Selected)[] instances) => new()
    {
        Instances = [.. instances.Select(instance => new ToolInstanceEntry { Name = instance.Name, IsSelected = instance.Selected })],
    };
}
