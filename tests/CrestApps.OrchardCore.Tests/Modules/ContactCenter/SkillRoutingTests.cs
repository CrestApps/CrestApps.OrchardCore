using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Skills were bare tags matched all-or-nothing: an agent either had "Spanish" or did not, so a queue could not
/// say how much Spanish it needed, could not prefer a skill without demanding it, and could not let the
/// requirement go once a caller had waited long enough. The last of those is the one that hurts — a caller who
/// needs a specialist nobody has available waits forever rather than reaching a generalist.
/// </summary>
public sealed class SkillRoutingTests
{
    private static readonly DateTime _now = new(2026, 3, 4, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Required_RejectsAnAgentBelowTheMinimumProficiency()
    {
        // Arrange
        var queue = Queue(new QueueSkillRequirement { SkillId = "spanish", MinimumProficiency = 3, Required = true });
        var novice = Agent("a1", ("spanish", 1));
        var fluent = Agent("a2", ("spanish", 4));

        // Act
        var context = await ApplyAsync(new RequiredSkillsRoutingStrategy(new StubClock(_now)), queue, Item(_now), novice, fluent);

        // Assert
        Assert.False(context.Candidates[0].IsEligible);
        Assert.True(context.Candidates[1].IsEligible);
    }

    [Fact]
    public async Task Required_AcceptsAnAgentExactlyAtTheMinimum()
    {
        // Arrange
        var queue = Queue(new QueueSkillRequirement { SkillId = "spanish", MinimumProficiency = 3, Required = true });

        // Act
        var context = await ApplyAsync(new RequiredSkillsRoutingStrategy(new StubClock(_now)), queue, Item(_now), Agent("a1", ("spanish", 3)));

        // Assert
        Assert.True(context.Candidates[0].IsEligible);
    }

    [Fact]
    public async Task Required_RelaxesOnceTheCallerHasWaitedLongEnough()
    {
        // Arrange
        // The point of relaxation: after the window, reaching a generalist beats waiting indefinitely for a
        // specialist who is not there.
        var queue = Queue(new QueueSkillRequirement
        {
            SkillId = "spanish",
            MinimumProficiency = 3,
            Required = true,
            RelaxAfterSeconds = 120,
        });

        var novice = Agent("a1", ("spanish", 1));

        // Act
        var context = await ApplyAsync(new RequiredSkillsRoutingStrategy(new StubClock(_now)), queue, Item(_now.AddSeconds(-121)), novice);

        // Assert
        Assert.True(context.Candidates[0].IsEligible);
        Assert.Contains(context.Candidates[0].Reasons, reason => reason.Contains("relax", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Required_DoesNotRelaxBeforeTheWindow()
    {
        // Arrange
        var queue = Queue(new QueueSkillRequirement
        {
            SkillId = "spanish",
            MinimumProficiency = 3,
            Required = true,
            RelaxAfterSeconds = 120,
        });

        // Act
        var context = await ApplyAsync(new RequiredSkillsRoutingStrategy(new StubClock(_now)), queue, Item(_now.AddSeconds(-60)), Agent("a1", ("spanish", 1)));

        // Assert
        Assert.False(context.Candidates[0].IsEligible);
    }

    [Fact]
    public async Task Required_NeverRelaxes_WhenNoWindowIsConfigured()
    {
        // Arrange
        // A requirement with no window is one the queue meant absolutely; relaxing it after some default would
        // silently route a regulated call to somebody not qualified to take it.
        var queue = Queue(new QueueSkillRequirement { SkillId = "spanish", MinimumProficiency = 3, Required = true });

        // Act
        var context = await ApplyAsync(new RequiredSkillsRoutingStrategy(new StubClock(_now)), queue, Item(_now.AddHours(-5)), Agent("a1", ("spanish", 1)));

        // Assert
        Assert.False(context.Candidates[0].IsEligible);
    }

    [Fact]
    public async Task Required_ReadsLegacyTags_AsRequiredAtProficiencyThree()
    {
        // Arrange
        // Existing queues and agents carry bare tags. Treating a tagged agent as unskilled would make every
        // skilled queue unroutable the moment this shipped.
        var queue = new ActivityQueue { ItemId = "q1", RequiredSkills = ["spanish"] };
        var tagged = new AgentProfile { ItemId = "a1", Skills = ["spanish"] };
        var untagged = new AgentProfile { ItemId = "a2" };

        // Act
        var context = await ApplyAsync(new RequiredSkillsRoutingStrategy(new StubClock(_now)), queue, Item(_now), tagged, untagged);

        // Assert
        Assert.True(context.Candidates[0].IsEligible);
        Assert.False(context.Candidates[1].IsEligible);
    }

    [Fact]
    public async Task Preferred_DoesNotRejectAnAgentWhoLacksTheSkill()
    {
        // Arrange
        var queue = Queue(new QueueSkillRequirement { SkillId = "billing", MinimumProficiency = 3, Required = false });

        // Act
        var context = await ApplyAsync(new PreferredSkillsRoutingStrategy(), queue, Item(_now), Agent("a1", ("spanish", 5)));

        // Assert
        Assert.True(context.Candidates[0].IsEligible);
    }

    [Fact]
    public async Task Preferred_ScoresTheAgentWhoHasTheSkillAboveTheOneWhoDoesNot()
    {
        // Arrange
        var queue = Queue(new QueueSkillRequirement { SkillId = "billing", MinimumProficiency = 3, Required = false });
        var withSkill = Agent("a1", ("billing", 3));
        var without = Agent("a2", ("spanish", 5));

        // Act
        var context = await ApplyAsync(new PreferredSkillsRoutingStrategy(), queue, Item(_now), withSkill, without);

        // Assert
        Assert.True(context.Candidates[0].Score > context.Candidates[1].Score);
    }

    [Fact]
    public async Task Preferred_ScoresHigherProficiencyAbove()
    {
        // Arrange
        // Two agents who both qualify are not equally good at it, and the queue said it cares about this skill.
        var queue = Queue(new QueueSkillRequirement { SkillId = "billing", MinimumProficiency = 2, Required = false });
        var expert = Agent("a1", ("billing", 5));
        var adequate = Agent("a2", ("billing", 2));

        // Act
        var context = await ApplyAsync(new PreferredSkillsRoutingStrategy(), queue, Item(_now), expert, adequate);

        // Assert
        Assert.True(context.Candidates[0].Score > context.Candidates[1].Score);
    }

    private static async Task<ActivityRoutingContext> ApplyAsync(
        IActivityRoutingStrategy strategy,
        ActivityQueue queue,
        QueueItem item,
        params AgentProfile[] agents)
    {
        var context = new ActivityRoutingContext(
            queue,
            item,
            agents.Select(agent => new ActivityRoutingCandidate(agent)).ToList());

        await strategy.ApplyAsync(context, TestContext.Current.CancellationToken);

        return context;
    }

    private static ActivityQueue Queue(params QueueSkillRequirement[] requirements)
    {
        var queue = new ActivityQueue { ItemId = "q1" };

        foreach (var requirement in requirements)
        {
            queue.SkillRequirements.Add(requirement);
        }

        return queue;
    }

    private static QueueItem Item(DateTime enqueuedUtc)
        => new() { ItemId = "i1", QueueId = "q1", EnqueuedUtc = enqueuedUtc };

    private static AgentProfile Agent(string agentId, params (string SkillId, int Proficiency)[] skills)
    {
        var agent = new AgentProfile { ItemId = agentId };

        foreach (var (skillId, proficiency) in skills)
        {
            agent.SkillProficiencies.Add(new AgentSkill { SkillId = skillId, Proficiency = proficiency });
        }

        return agent;
    }
}
