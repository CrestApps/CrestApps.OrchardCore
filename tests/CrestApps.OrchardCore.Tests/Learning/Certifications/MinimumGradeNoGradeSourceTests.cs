using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Certifications.Core.Requirements;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Tests.Learning.Certifications;

/// <summary>
/// R9-M3: a missing course grade meets a minimum grade only when nothing in the course gave the learner a grade, never
/// for an imported enrollment or a completion recorded without the course's outline.
/// </summary>
public sealed class MinimumGradeNoGradeSourceTests
{
    [Fact]
    public void AnImportedCompletedEnrollmentWithNoGrade_DoesNotMeetACertificatesMinimumGrade()
    {
        // Arrange: an enrollment a recipe imported as completed, with no grade and no completion record.
        var imported = new Enrollment { Status = EnrollmentStatus.Completed, CompletedUtc = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc) };

        // Act
        var meets = CourseCompletedRequirement.MeetsMinimumGrade(imported, 70);

        // Assert
        Assert.False(meets);
    }

    [Fact]
    public void ACompletionWithNoGradeButAGradeSource_DoesNotMeetACertificatesMinimumGrade()
    {
        // Arrange: a completion recorded with no grade, not because nothing in the course gives one (a course that
        // couldn't be loaded, or a completion from before grades were recorded).
        var enrollment = new Enrollment
        {
            Status = EnrollmentStatus.Completed,
            CompletedUtc = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc),
            Completion = new CompletionSnapshot { Grade = null, NoGradeSource = false },
        };

        // Act
        var meets = CourseCompletedRequirement.MeetsMinimumGrade(enrollment, 70);

        // Assert
        Assert.False(meets);
    }

    [Theory]
    [InlineData(null, true, true)]
    [InlineData(80.0, false, true)]
    [InlineData(60.0, false, false)]
    public void ACompletion_MeetsACertificatesMinimumGrade_ByItsGrade_OrWhenNothingInTheCourseGaveOne(double? grade, bool noGradeSource, bool expected)
    {
        // Arrange
        var enrollment = new Enrollment
        {
            Status = EnrollmentStatus.Completed,
            CompletedUtc = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc),
            Completion = new CompletionSnapshot { Grade = (decimal?)grade, NoGradeSource = noGradeSource },
        };

        // Act
        var meets = CourseCompletedRequirement.MeetsMinimumGrade(enrollment, 70);

        // Assert
        Assert.Equal(expected, meets);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task TheMinimumGradeCriterion_WithNoGrade_IsMetOnlyWhenNothingInTheCourseGivesOne(bool noGradeSource, bool expected)
    {
        // Arrange
        var context = new CompletionCriterionContext
        {
            Settings = new LearningRuleSettings { Name = LearningRuleNames.MinimumGrade, Values = { ["Grade"] = "70" } },
            Grade = null,
            NoGradeSource = noGradeSource,
        };

        // Act
        var met = await new MinimumGradeCriterion().IsMetAsync(context);

        // Assert
        Assert.Equal(expected, met);
    }

    [Fact]
    public void ACourseThatCouldNotBeLoaded_HasAGradeSource_SoItsMissingGradeIsNoGrade()
    {
        // Arrange: the empty outline a completion uses when the course can't be loaded.
        var outline = new CourseOutline();

        // Act
        var noGradeSource = CourseGradeCalculator.HasNoGradeSource(outline, progress: null);

        // Assert
        Assert.False(noGradeSource);
    }
}
