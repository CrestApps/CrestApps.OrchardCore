using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Lti;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Lti;

/// <summary>
/// The id a tool knows a person by is stable per tool and person, different for each tool and never the user id; the
/// LIS roles follow the person's place in the course.
/// </summary>
public sealed class LtiSubjectsAndRolesTests
{
    [Fact]
    public void For_IsStablePerToolAndPerson_AndNotTheUserId()
    {
        // Arrange
        var tool = new ExternalTool { ItemId = "tool-1", SubjectSalt = LtiSubjects.NewSalt() };

        // Act
        var first = LtiSubjects.For(tool, "4kq9z8user");
        var again = LtiSubjects.For(new ExternalTool { ItemId = "tool-1", SubjectSalt = tool.SubjectSalt }, "4kq9z8user");
        var other = LtiSubjects.For(tool, "another-user");

        // Assert
        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
        Assert.DoesNotContain("4kq9z8user", first, StringComparison.Ordinal);
        Assert.Equal(43, first.Length);
        Assert.DoesNotContain('=', first);
        Assert.DoesNotContain('+', first);
        Assert.DoesNotContain('/', first);
    }

    [Fact]
    public void For_DiffersBetweenTools()
    {
        // Arrange
        var one = new ExternalTool { ItemId = "tool-1", SubjectSalt = LtiSubjects.NewSalt() };
        var two = new ExternalTool { ItemId = "tool-2", SubjectSalt = LtiSubjects.NewSalt() };

        // Act and assert
        Assert.NotEqual(LtiSubjects.For(one, "user-1"), LtiSubjects.For(two, "user-1"));
    }

    [Fact]
    public void For_AToolWithoutASalt_Throws()
        => Assert.Throws<InvalidOperationException>(() => LtiSubjects.For(new ExternalTool { ItemId = "tool-1" }, "user-1"));

    [Fact]
    public void Roles_ALearner_IsLearner()
        => Assert.Equal([LtiConstants.Roles.Learner], LtiRoles.For(learner: true, [], courseManager: false));

    [Fact]
    public void Roles_TheStaff_MapToInstructorAndItsSubRolesWithTheirPrincipalRole()
    {
        // Act
        var instructor = LtiRoles.For(false, [LearningConstants.CourseStaffRoles.Instructor], false);
        var assistant = LtiRoles.For(false, [LearningConstants.CourseStaffRoles.Assistant], false);
        var grader = LtiRoles.For(false, [LearningConstants.CourseStaffRoles.Grader], false);
        var observer = LtiRoles.For(false, [LearningConstants.CourseStaffRoles.Observer], false);

        // Assert
        Assert.Equal([LtiConstants.Roles.Instructor], instructor);
        Assert.Equal([LtiConstants.Roles.Instructor, LtiConstants.Roles.TeachingAssistant], assistant);
        Assert.Equal([LtiConstants.Roles.Instructor, LtiConstants.Roles.Grader], grader);
        Assert.Equal([LtiConstants.Roles.Mentor], observer);
    }

    [Fact]
    public void Roles_ACourseManagerNotOnTheStaff_IsAdministratorAndInstructor()
        => Assert.Equal([LtiConstants.Roles.ContextAdministrator, LtiConstants.Roles.Instructor], LtiRoles.For(false, [], courseManager: true));

    [Fact]
    public void Roles_AreNotRepeated()
    {
        // Act
        var roles = LtiRoles.For(true, [LearningConstants.CourseStaffRoles.Instructor, LearningConstants.CourseStaffRoles.Grader, LearningConstants.CourseStaffRoles.Instructor], courseManager: true);

        // Assert
        Assert.Equal([LtiConstants.Roles.Instructor, LtiConstants.Roles.Grader, LtiConstants.Roles.Learner], roles);
    }
}
