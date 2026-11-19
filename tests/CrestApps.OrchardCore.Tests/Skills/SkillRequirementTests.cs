using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Skills;

/// <summary>
/// A person holds what a piece of work needs when they hold every required skill at its lowest proficiency on the day;
/// a requirement without a skill is ignored, so no requirements means everybody qualifies, and a proficiency below one
/// asks for one.
/// </summary>
public sealed class SkillRequirementTests
{
    private static readonly SkillSubject _technician = new(SkillSubjectKind.User, "tech-1");
    private static readonly DateOnly _day = new(2026, 11, 13);

    [Fact]
    public async Task EveryRequirement_MustBeHeldAtItsLevel()
    {
        // Arrange
        var skills = Skills(("boilers", 3), ("gas", 1));

        // Act
        var holds = await skills.HoldsAllAsync(_technician, [new SkillRequirement { SkillId = "boilers", MinProficiency = 2 }, new SkillRequirement { SkillId = "gas" }], _day, TestContext.Current.CancellationToken);
        var tooLow = await skills.HoldsAllAsync(_technician, [new SkillRequirement { SkillId = "boilers", MinProficiency = 4 }], _day, TestContext.Current.CancellationToken);
        var missing = await skills.HoldsAllAsync(_technician, [new SkillRequirement { SkillId = "gas" }, new SkillRequirement { SkillId = "electrical" }], _day, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(holds);
        Assert.False(tooLow);
        Assert.False(missing);
    }

    [Fact]
    public async Task NoRequirements_OrOnesWithoutASkill_QualifyEverybody_AndALevelBelowOneAsksForOne()
    {
        // Arrange
        var skills = Skills(("gas", 1));

        // Act
        var none = await skills.HoldsAllAsync(_technician, null, _day, TestContext.Current.CancellationToken);
        var blank = await skills.HoldsAllAsync(_technician, [new SkillRequirement { SkillId = " " }, null, new SkillRequirement()], _day, TestContext.Current.CancellationToken);
        var zero = await skills.HoldsAllAsync(_technician, [new SkillRequirement { SkillId = "gas", MinProficiency = 0 }], _day, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(none);
        Assert.True(blank);
        Assert.True(zero);
    }

    private static ISkillAssignmentService Skills(params (string SkillId, int Proficiency)[] held)
    {
        var service = new Mock<ISkillAssignmentService>();

        service
            .Setup(value => value.HoldsAsync(It.IsAny<SkillSubject>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SkillSubject _, string skillId, int minimum, DateOnly _, CancellationToken _) => held.Any(skill => skill.SkillId == skillId && skill.Proficiency >= minimum));

        return service.Object;
    }
}
