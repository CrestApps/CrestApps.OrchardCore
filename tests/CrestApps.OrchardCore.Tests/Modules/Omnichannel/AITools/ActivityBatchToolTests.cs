using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.AI;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Tests.Agent;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.AITools;

/// <summary>
/// The activity load tools list, copy, count, start and delete loads with the Load Activities page's rules.
/// </summary>
public sealed class ActivityBatchToolTests
{
    private readonly OmnichannelToolHarness _harness = new();

    private OmnichannelActivityBatch AddBatch(OmnichannelActivityBatchStatus status = OmnichannelActivityBatchStatus.New)
        => _harness.Batches.Add(new OmnichannelActivityBatch
        {
            ItemId = "batch-1",
            DisplayText = "Renewals May",
            Source = ActivitySources.Manual,
            CampaignId = "campaign-1",
            Channel = "Phone",
            Status = status,
            UserIds = ["user-1"],
            TotalLoaded = status == OmnichannelActivityBatchStatus.Loaded ? 40 : null,
        });

    [Fact]
    public async Task List_ReturnsTheLoadsWithStatusAndTotals()
    {
        // Arrange
        AddBatch(OmnichannelActivityBatchStatus.Loaded);

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListActivityBatchesTool(), _harness.Build()));

        // Assert
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());
        Assert.Equal("Loaded", item.GetProperty("status").GetString());
        Assert.Equal(40, item.GetProperty("totals").GetProperty("loaded").GetInt64());
        Assert.False(item.TryGetProperty("definition", out _));
    }

    [Fact]
    public async Task List_ReturnsTheDefinitionOfOneLoad()
    {
        // Arrange
        AddBatch();

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListActivityBatchesTool(), _harness.Build(), new() { ["id"] = "batch-1" }));

        // Assert
        Assert.Equal("user-1", Assert.Single(json.GetProperty("definition").GetProperty("assignedUserIds").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task Save_CopiesALoadAsANewUnloadedLoad()
    {
        // Arrange
        AddBatch(OmnichannelActivityBatchStatus.Loaded);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveActivityBatchTool(), _harness.Build(), new()
        {
            ["cloneFromId"] = "batch-1",
            ["displayText"] = "Renewals June",
        });

        // Assert
        Assert.Equal("created", AgentToolTestContext.ReadJson(result).GetProperty("saved").GetString());
        var copy = Assert.Single(_harness.Batches.Created);
        Assert.Equal("Renewals June", copy.DisplayText);
        Assert.Equal("campaign-1", copy.CampaignId);
        Assert.Equal(OmnichannelActivityBatchStatus.New, copy.Status);
        Assert.NotEqual("batch-1", copy.ItemId);
    }

    [Fact]
    public async Task Save_SaysSoWhenTheSourceDoesNotExist()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveActivityBatchTool(), _harness.Build(), new() { ["cloneFromId"] = "missing" });

        // Assert
        Assert.Contains("no activity load has the ID 'missing'", result);
        Assert.Empty(_harness.Batches.Created);
    }

    [Fact]
    public async Task Preview_CountsACopyOfTheSavedLoad()
    {
        // Arrange
        var batch = AddBatch();
        OmnichannelActivityBatch counted = null;
        _harness.LoadCoordinator
            .Setup(coordinator => coordinator.PreviewAsync(It.IsAny<OmnichannelActivityBatch>(), It.IsAny<CancellationToken>()))
            .Callback((OmnichannelActivityBatch value, CancellationToken _) => counted = value)
            .ReturnsAsync(new ActivityBatchLoadPreview { Matched = 120, WouldLoad = 100, SkippedAsOptedOut = 20 });

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new PreviewActivityBatchTool(), _harness.Build(), new() { ["id"] = "batch-1" }));

        // Assert
        Assert.Equal(100, json.GetProperty("wouldLoad").GetInt64());
        Assert.Equal(20, json.GetProperty("skipped").GetProperty("optedOut").GetInt64());
        Assert.NotSame(batch, counted);
        Assert.Equal("batch-1", counted.ItemId);
    }

    [Fact]
    public async Task Preview_SaysSoWhenNoLoadHasTheId()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new PreviewActivityBatchTool(), _harness.Build(), new() { ["id"] = "missing" });

        // Assert
        Assert.Contains("no activity load has the ID 'missing'", result);
    }

    [Theory]
    [InlineData(OmnichannelActivityBatchStatus.Loaded, "can't be loaded again")]
    [InlineData(OmnichannelActivityBatchStatus.Loading, "is already being loaded")]
    [InlineData(OmnichannelActivityBatchStatus.Started, "is already being loaded")]
    public async Task Load_RefusesALoadThatRanOrIsRunning(OmnichannelActivityBatchStatus status, string expected)
    {
        // Arrange
        var batch = AddBatch(status);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new LoadActivityBatchTool(), _harness.Build(), new() { ["id"] = "batch-1" });

        // Assert
        Assert.Contains(expected, result);
        Assert.Empty(_harness.Batches.Updated);
        Assert.Equal(status, batch.Status);
    }

    [Fact]
    public async Task Load_DoesNotMarkTheLoadStartedWhenItCannotBeQueued()
    {
        // Arrange: no shell scope, so nothing could run the load after the turn.
        var batch = AddBatch();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new LoadActivityBatchTool(), _harness.Build(), new() { ["id"] = "batch-1" });

        // Assert
        Assert.Contains("could not be started here", result);
        Assert.Equal(OmnichannelActivityBatchStatus.New, batch.Status);
        Assert.Empty(_harness.Batches.Updated);
        _harness.LoadCoordinator.Verify(coordinator => coordinator.LoadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_RemovesALoadThatHasNotRun()
    {
        // Arrange
        AddBatch();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteActivityBatchTool(), _harness.Build(), new() { ["id"] = "batch-1" });

        // Assert
        Assert.Equal("batch-1", AgentToolTestContext.ReadJson(result).GetProperty("deleted").GetProperty("id").GetString());
        Assert.Empty(_harness.Batches.Items);
    }

    [Fact]
    public async Task Delete_RefusesALoadThatIsLoading()
    {
        // Arrange
        AddBatch(OmnichannelActivityBatchStatus.Loading);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteActivityBatchTool(), _harness.Build(), new() { ["id"] = "batch-1" });

        // Assert
        Assert.Contains("is being loaded and can't be removed", result);
        Assert.Single(_harness.Batches.Items);
    }

    [Fact]
    public async Task Delete_RefusesALoadedBatchWithoutDeleteLoadedActivityBatches()
    {
        // Arrange
        AddBatch(OmnichannelActivityBatchStatus.Loaded);
        var services = _harness.Build(authorize: (permission, _) => permission.Name != OmnichannelConstants.Permissions.DeleteLoadedActivityBatches.Name);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteActivityBatchTool(), services, new() { ["id"] = "batch-1" });

        // Assert
        Assert.Contains("you are not allowed to remove a loaded batch", result);
        Assert.Single(_harness.Batches.Items);
    }
}
