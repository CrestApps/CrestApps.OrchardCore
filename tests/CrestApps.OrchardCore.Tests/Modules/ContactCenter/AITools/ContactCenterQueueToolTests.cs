using CrestApps.OrchardCore.ContactCenter.AI;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Agent;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AITools;

/// <summary>
/// The queue save and delete tools go through the real queue manager and handlers, apply the queue editor's rules,
/// change only what they are given and keep a record that routing still uses. Queues are read, and queue groups read
/// and written, with the generic record tools (see <see cref="ContactCenterRecordKindTests"/>).
/// </summary>
public sealed class ContactCenterQueueToolTests
{
    private readonly ContactCenterSetupToolHarness _harness = new();

    [Fact]
    public async Task SaveQueue_CreatesAQueueWithItsOverflowAndTheEditorRules()
    {
        // Arrange
        _harness.AddQueue("general", "General");
        _harness.QueueGroups.Entries.Add(new ActivityQueueGroup { ItemId = "support", Name = "Support" });

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveContactCenterQueueTool(), _harness.Build(), new()
        {
            ["name"] = " Billing ",
            ["queueGroupId"] = "support",
            ["slaThresholdSeconds"] = 30,
            ["requiredSkills"] = new[] { "Spanish", "spanish" },
            ["overflowTargets"] = new[]
            {
                new Dictionary<string, object> { ["queueId"] = "general", ["afterSeconds"] = 120 },
                new Dictionary<string, object> { ["queueId"] = "general", ["afterSeconds"] = 300 },
            },
            ["maxWaitSeconds"] = 600,
            ["maxWaitAction"] = "Overflow",
            ["treatment"] = new Dictionary<string, object> { ["callbackDtmfKey"] = "1", ["minimumEstimateSeconds"] = 90, ["maximumEstimateSeconds"] = 60 },
        }));

        // Assert
        Assert.Equal("created", json.GetProperty("saved").GetString());
        var queue = _harness.Queues.Entries.Single(entry => entry.Name == "Billing");
        Assert.Equal("support", queue.QueueGroupId);
        Assert.Equal(30, queue.SlaThresholdSeconds);
        Assert.Single(queue.RequiredSkills);
        var target = Assert.Single(queue.OverflowTargets);
        Assert.Equal(("general", 120), (target.QueueId, target.AfterSeconds));
        Assert.Equal(QueueMaxWaitAction.Overflow, queue.MaxWaitAction);
        Assert.Equal("1", queue.Treatment.CallbackDtmfKey);
        Assert.Equal(90, queue.Treatment.MaximumEstimateSeconds);
        Assert.Equal(ContactCenterSetupToolHarness.Now, queue.CreatedUtc);
    }

    [Fact]
    public async Task SaveQueue_RefusesWhatTheQueueHandlerRefuses()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveContactCenterQueueTool(), _harness.Build(), new()
        {
            ["name"] = "Billing",
            ["maxWaitSeconds"] = 600,
            ["maxWaitAction"] = "Overflow",
            ["treatment"] = new Dictionary<string, object> { ["callbackDtmfKey"] = "12" },
        });

        // Assert
        Assert.StartsWith("Nothing was saved:", result);
        Assert.Contains("names no overflow queue", result);
        Assert.Contains("single telephone key", result);
        Assert.Empty(_harness.Queues.Entries);
    }

    [Fact]
    public async Task SaveQueue_ChangesOnlyTheFieldsGivenAndKeepsTheRestOfTheTreatment()
    {
        // Arrange
        _harness.AddQueue("queue-1", "Billing", queue =>
        {
            queue.RoutingStrategy = QueueRoutingStrategy.RoundRobin;
            queue.SlaThresholdSeconds = 45;
            queue.Treatment = new QueueTreatmentSettings { WelcomeMessage = "Welcome", AnnouncePosition = true };
            queue.Properties["Other"] = "kept";
        });

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveContactCenterQueueTool(), _harness.Build(), new()
        {
            ["id"] = "queue-1",
            ["enabled"] = false,
            ["treatment"] = new Dictionary<string, object> { ["announceEstimatedWait"] = true },
        }));

        // Assert
        Assert.Equal("updated", json.GetProperty("saved").GetString());
        var queue = _harness.Queues.Entries.Single();
        Assert.False(queue.Enabled);
        Assert.Equal(QueueRoutingStrategy.RoundRobin, queue.RoutingStrategy);
        Assert.Equal(45, queue.SlaThresholdSeconds);
        Assert.Equal("Welcome", queue.Treatment.WelcomeMessage);
        Assert.True(queue.Treatment.AnnouncePosition);
        Assert.True(queue.Treatment.AnnounceEstimatedWait);
        Assert.Equal("kept", queue.Properties["Other"]?.ToString());
        Assert.Equal(ContactCenterSetupToolHarness.Now, queue.ModifiedUtc);
    }

    [Fact]
    public async Task SaveQueue_RefusesAnOverflowQueueThatDoesNotExist()
    {
        // Arrange
        _harness.AddQueue("queue-1", "Billing");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveContactCenterQueueTool(), _harness.Build(), new()
        {
            ["id"] = "queue-1",
            ["overflowQueueId"] = "missing",
        });

        // Assert
        Assert.Equal("Nothing was saved: No queue has the ID 'missing' to overflow to.", result);
        Assert.Null(_harness.Queues.Entries.Single().OverflowQueueId);
    }

    [Fact]
    public async Task DeleteRecord_KeepsAQueueThatAnotherQueueOverflowsTo()
    {
        // Arrange
        _harness.AddQueue("general", "General");
        _harness.AddQueue("billing", "Billing", queue => queue.OverflowQueueId = "general");

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new DeleteContactCenterRecordTool(), _harness.Build(), new()
        {
            ["kind"] = "queue",
            ["id"] = "general",
        }));

        // Assert
        Assert.False(json.GetProperty("deleted").GetBoolean());
        Assert.Contains("Billing", json.GetProperty("usedBy")[0].GetString());
        Assert.Equal(2, _harness.Queues.Entries.Count);
    }

    [Fact]
    public async Task DeleteRecord_DeletesAQueueWhenToldTo()
    {
        // Arrange
        _harness.AddQueue("general", "General");
        _harness.AddQueue("billing", "Billing", queue => queue.OverflowQueueId = "general");

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new DeleteContactCenterRecordTool(), _harness.Build(), new()
        {
            ["kind"] = "queue",
            ["id"] = "general",
            ["deleteEvenIfUsed"] = true,
        }));

        // Assert
        Assert.True(json.GetProperty("deleted").GetBoolean());
        Assert.Equal("billing", Assert.Single(_harness.Queues.Entries).ItemId);
    }

    [Fact]
    public async Task DeleteRecord_ChecksThePermissionOfTheKind()
    {
        // Arrange
        _harness.AddCalendar("calendar-1", "Support");
        var services = _harness.Build(authorize: (permission, _) => permission.Name == ContactCenterPermissions.ManageQueues.Name);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteContactCenterRecordTool(), services, new()
        {
            ["kind"] = "businessHoursCalendar",
            ["id"] = "calendar-1",
        });

        // Assert
        Assert.Equal(ContactCenterSetupToolBase.NotAllowedMessage, result);
        Assert.Single(_harness.Calendars.Entries);
    }

    [Fact]
    public async Task DeleteRecord_SaysSoWhenTheRecordDoesNotExist()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteContactCenterRecordTool(), _harness.Build(), new()
        {
            ["kind"] = "entryPoint",
            ["id"] = "missing",
        });

        // Assert
        Assert.Equal("Nothing was deleted: no entry point has the ID 'missing'.", result);
    }
}
