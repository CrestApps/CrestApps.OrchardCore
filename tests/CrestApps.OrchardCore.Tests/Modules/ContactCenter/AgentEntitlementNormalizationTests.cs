using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The skill proficiencies and queue preferences a manager sets on the entitlement screen (or ships in a
/// recipe) feed the routing strategies and the cross-queue selector directly, so what is stored must be clean:
/// one row per skill or queue, values inside the supported range, and the plain skill tag list kept in step.
/// </summary>
public sealed class AgentEntitlementNormalizationTests
{
    private static readonly DateTime _now = new(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NormalizeSkillProficiencies_KeepsOneRowPerSkill_AndClampsTheProficiency()
    {
        // Act
        var skills = AgentEntitlementUtilities.NormalizeSkillProficiencies(
        [
            new AgentSkill { SkillId = "  Spanish ", Proficiency = 9 },
            new AgentSkill { SkillId = "spanish", Proficiency = 2 },
            new AgentSkill { SkillId = "French", Proficiency = 0 },
            new AgentSkill { SkillId = "   ", Proficiency = 3 },
            null,
        ]);

        // Assert
        Assert.Collection(
            skills,
            skill =>
            {
                Assert.Equal("Spanish", skill.SkillId);
                Assert.Equal(AgentSkill.MaximumProficiency, skill.Proficiency);
            },
            skill =>
            {
                Assert.Equal("French", skill.SkillId);
                Assert.Equal(AgentSkill.MinimumProficiency, skill.Proficiency);
            });
    }

    [Fact]
    public void NormalizeQueueMemberships_KeepsOneRowPerQueue_AndNoNegativeValues()
    {
        // Act
        var memberships = AgentEntitlementUtilities.NormalizeQueueMemberships(
        [
            new AgentQueueMembership { QueueId = " q1 ", Priority = -1, DelaySeconds = 30 },
            new AgentQueueMembership { QueueId = "Q1", Priority = 5, DelaySeconds = 0 },
            new AgentQueueMembership { QueueId = "q2", Priority = 1, DelaySeconds = -10 },
            new AgentQueueMembership { QueueId = "", Priority = 1, DelaySeconds = 1 },
            null,
        ]);

        // Assert
        Assert.Collection(
            memberships,
            membership =>
            {
                Assert.Equal("q1", membership.QueueId);
                Assert.Equal(0, membership.Priority);
                Assert.Equal(30, membership.DelaySeconds);
            },
            membership =>
            {
                Assert.Equal("q2", membership.QueueId);
                Assert.Equal(1, membership.Priority);
                Assert.Equal(0, membership.DelaySeconds);
            });
    }

    [Fact]
    public async Task UpdateEntitlementsAsync_StoresProficienciesAndPreferences_AndDerivesTheSkillTags()
    {
        // Arrange
        // The entitlement screen edits skills as proficiency rows; every reader of "does the agent have this
        // skill" looks at the tag list, so it must be derived from those rows rather than left stale.
        var existing = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            AllowedQueueIds = ["q1", "q2"],
            QueueIds = ["q1", "q2"],
            Skills = ["old-skill"],
            SkillProficiencies = [new AgentSkill { SkillId = "old-skill", Proficiency = 4 }],
        };

        var service = CreateService(existing);

        // Act
        var profile = await service.UpdateEntitlementsAsync(
            "a1",
            new AgentEntitlements
            {
                AllowedQueueIds = ["q1", "q2"],
                AllowedCampaignIds = [],
                SkillProficiencies = [new AgentSkill { SkillId = "spanish", Proficiency = 5 }, new AgentSkill { SkillId = "french", Proficiency = 2 }],
                QueueMemberships = [new AgentQueueMembership { QueueId = "q2", Priority = 1, DelaySeconds = 45 }],
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["spanish", "french"], profile.Skills);
        Assert.Collection(
            profile.SkillProficiencies,
            skill => Assert.Equal(("spanish", 5), (skill.SkillId, skill.Proficiency)),
            skill => Assert.Equal(("french", 2), (skill.SkillId, skill.Proficiency)));
        var membership = Assert.Single(profile.QueueMemberships);
        Assert.Equal(("q2", 1, 45), (membership.QueueId, membership.Priority, membership.DelaySeconds));
        Assert.Equal(["q1", "q2"], profile.QueueIds);
    }

    [Fact]
    public async Task ApplyManagedConfigurationAsync_WithoutProficiencies_LeavesExistingProficienciesAlone()
    {
        // Arrange
        // A recipe written before proficiencies existed ships only the tag list. It must not wipe the levels a
        // manager has since set, only bring the tag list up to date.
        var existing = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            Skills = ["spanish"],
            SkillProficiencies = [new AgentSkill { SkillId = "spanish", Proficiency = 5 }],
            QueueMemberships = [new AgentQueueMembership { QueueId = "q1", Priority = 2 }],
        };

        var service = CreateService(existing);

        // Act
        var profile = await service.ApplyManagedConfigurationAsync(
            "a1",
            new AgentManagedConfiguration { DisplayName = "Agent", AllowedQueueIds = ["q1"], AllowedCampaignIds = [], Skills = ["spanish", "french"] },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["spanish", "french"], profile.Skills);
        var skill = Assert.Single(profile.SkillProficiencies);
        Assert.Equal(("spanish", 5), (skill.SkillId, skill.Proficiency));
        var membership = Assert.Single(profile.QueueMemberships);
        Assert.Equal(2, membership.Priority);
    }

    [Fact]
    public async Task ApplyManagedConfigurationAsync_WithProficiencies_UnionsThemIntoTheSkillTags()
    {
        // Arrange
        var existing = new AgentProfile { ItemId = "a1", UserId = "u1" };
        var service = CreateService(existing);

        // Act
        var profile = await service.ApplyManagedConfigurationAsync(
            "a1",
            new AgentManagedConfiguration
            {
                DisplayName = "Agent",
                AllowedQueueIds = ["q1"],
                AllowedCampaignIds = [],
                Skills = ["billing"],
                SkillProficiencies = [new AgentSkill { SkillId = "spanish", Proficiency = 4 }],
                QueueMemberships = [new AgentQueueMembership { QueueId = "q1", Priority = 0, DelaySeconds = 20 }],
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["billing", "spanish"], profile.Skills);
        var skill = Assert.Single(profile.SkillProficiencies);
        Assert.Equal(("spanish", 4), (skill.SkillId, skill.Proficiency));
        var membership = Assert.Single(profile.QueueMemberships);
        Assert.Equal(("q1", 20), (membership.QueueId, membership.DelaySeconds));
    }

    private static AgentPresenceManagerService CreateService(AgentProfile existing)
    {
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync(existing.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((Mock.Of<ILocker>(), true));

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new AgentPresenceManagerService(
            agentManager.Object,
            [],
            new NoAgentWorkStateHealingService(),
            new EnforcingAgentEntitlementPolicy(),
            new Mock<IContactCenterEventPublisher>().Object,
            distributedLock.Object,
            clock.Object,
            new Mock<ILogger<AgentPresenceManagerService>>().Object);
    }
}
