using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Pins the one definition of when two skill names name the same skill, and the routing behaviour that
/// depends on it.
/// </summary>
public sealed class SkillTagTests
{
    [Theory]
    [InlineData("Spanish", "Spanish")]
    [InlineData(" Spanish", "Spanish")]
    [InlineData("Spanish ", "Spanish")]
    [InlineData("\tSpanish\r\n", "Spanish")]
    [InlineData("Tier 2 Support", "Tier 2 Support")]
    public void ASkillTag_KeepsTheNameWithoutItsSurroundingWhitespace(string name, string expected)
    {
        // Act
        var created = SkillTag.TryCreate(name, out var skillTag);

        // Assert
        Assert.True(created);
        Assert.Equal(expected, skillTag.Value);
        Assert.Equal(expected, skillTag.ToString());
        Assert.True(skillTag.HasValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void ASkillTag_IsRefused_WhenTheNameNamesNothing(string name)
    {
        // Act
        var created = SkillTag.TryCreate(name, out var skillTag);

        // Assert
        Assert.False(created);
        Assert.False(skillTag.HasValue);
        Assert.Throws<ArgumentException>(() => SkillTag.Create(name));
    }

    [Theory]
    [InlineData("Spanish", "spanish")]
    [InlineData("Spanish", " SPANISH ")]
    [InlineData("Tier 2 Support", "tier 2 support")]
    public void TwoSkillTags_AreEqual_WhenTheyNameTheSameSkill(string first, string second)
    {
        // Arrange
        var firstTag = SkillTag.Create(first);
        var secondTag = SkillTag.Create(second);

        // Assert
        Assert.Equal(firstTag, secondTag);
        Assert.Equal(firstTag.GetHashCode(), secondTag.GetHashCode());
        Assert.Single(new HashSet<SkillTag> { firstTag, secondTag });
    }

    [Fact]
    public void TwoSkillTags_AreNotEqual_WhenTheyNameDifferentSkills()
    {
        // Assert
        // Interior spacing is part of the name. "Tier2" and "Tier 2" are two different skills, and guessing
        // that they were meant to be the same would silently route work to an agent who is not qualified.
        Assert.NotEqual(SkillTag.Create("Tier 2"), SkillTag.Create("Tier2"));
    }

    [Fact]
    public void SkillTags_AreDeduplicated_InTheOrderTheyWereFirstNamed()
    {
        // Act
        var skillTags = SkillTag.CreateAll(["Spanish", " spanish ", null, "  ", "Billing", "SPANISH"]);

        // Assert
        Assert.Equal([SkillTag.Create("Spanish"), SkillTag.Create("Billing")], skillTags);
        Assert.Equal(["Spanish", "Billing"], SkillTag.NormalizeAll(["Spanish", " spanish ", null, "  ", "Billing", "SPANISH"]));
    }

    [Fact]
    public void SkillTags_AreEmpty_WhenNothingWasNamed()
    {
        // Assert
        Assert.Empty(SkillTag.CreateAll(null));
        Assert.Empty(SkillTag.NormalizeAll(null));
    }

    [Fact]
    public async Task AnAgent_MatchesARequiredSkill_EvenWhenTheStoredNameCarriesWhitespaceOrDifferentCasing()
    {
        // Arrange
        // Agent skills can be written by a recipe, a deployment, or an import, and none of those paths goes
        // through the administration form that trims what a queue requires. Before both sides were read
        // through the same type, this agent was silently ineligible for every activity on the queue.
        var queue = new ActivityQueue
        {
            ItemId = "q1",
            RequiredSkills = ["Spanish", "Billing"],
        };

        var agent = new AgentProfile
        {
            ItemId = "a1",
            Skills = [" spanish ", "BILLING"],
        };

        var context = new ActivityRoutingContext(queue, new QueueItem { ItemId = "i1", QueueId = "q1" }, [new ActivityRoutingCandidate(agent)]);

        // Act
        await new RequiredSkillsRoutingStrategy().ApplyAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var candidate = Assert.Single(context.Candidates);
        Assert.True(candidate.IsEligible);
    }

    [Fact]
    public async Task AnAgent_IsRejected_WhenARequiredSkillIsGenuinelyMissing()
    {
        // Arrange
        var queue = new ActivityQueue
        {
            ItemId = "q1",
            RequiredSkills = ["Spanish", "Billing"],
        };

        var agent = new AgentProfile
        {
            ItemId = "a1",
            Skills = ["Spanish"],
        };

        var context = new ActivityRoutingContext(queue, new QueueItem { ItemId = "i1", QueueId = "q1" }, [new ActivityRoutingCandidate(agent)]);

        // Act
        await new RequiredSkillsRoutingStrategy().ApplyAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var candidate = Assert.Single(context.Candidates);
        Assert.False(candidate.IsEligible);
        Assert.Contains(candidate.Reasons, reason => reason.Contains("Billing", StringComparison.Ordinal));
    }
}
