using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Programs;
using CrestApps.OrchardCore.Learning.Programs.Core;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Tests.Learning.Programs;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// Access as Moodle courses restrict it: an item open only to named people or to the members of an audience, and a
/// learning path that skips its hidden (unpublished) courses instead of waiting for them forever.
/// </summary>
public sealed class AccessRestrictionTests
{
    [Fact]
    public async Task PeopleRule_OpensTheItemOnlyToTheNamedPeople()
    {
        // Arrange
        var rule = new PeopleReleaseRule(new ServiceCollection().BuildServiceProvider());
        var settings = new LearningRuleSettings { Name = LearningRuleNames.People, Values = { ["UserIds"] = "ana, bo" } };

        // Act
        var ana = await rule.EvaluateAsync(Context(settings, "ana"));
        var bo = await rule.EvaluateAsync(Context(settings, "bo"));
        var cy = await rule.EvaluateAsync(Context(settings, "cy"));

        // Assert
        Assert.True(ana.IsReleased);
        Assert.True(bo.IsReleased);
        Assert.False(cy.IsReleased);
        Assert.Equal(LearningRefusals.Restricted, cy.Reason);
    }

    [Fact]
    public async Task PeopleRule_OpensTheItemToTheMembersOfTheAudience()
    {
        // Arrange
        var audiences = new Mock<IUserAudienceService>();
        audiences.Setup(service => service.IsMemberAsync("field-staff", "cy", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var provider = new ServiceCollection().AddSingleton(audiences.Object).BuildServiceProvider();
        var rule = new PeopleReleaseRule(provider);
        var settings = new LearningRuleSettings { Name = LearningRuleNames.People, Values = { ["UserIds"] = "ana", ["Audience"] = "field-staff" } };

        // Act
        var ana = await rule.EvaluateAsync(Context(settings, "ana"));
        var cy = await rule.EvaluateAsync(Context(settings, "cy"));
        var dee = await rule.EvaluateAsync(Context(settings, "dee"));

        // Assert
        Assert.True(ana.IsReleased);
        Assert.True(cy.IsReleased);
        Assert.False(dee.IsReleased);
    }

    [Fact]
    public async Task PeopleRule_WithNobodyNamed_OrAnAudienceWithoutTheFeature_StaysLocked()
    {
        // Arrange
        var rule = new PeopleReleaseRule(new ServiceCollection().BuildServiceProvider());
        var empty = new LearningRuleSettings { Name = LearningRuleNames.People };
        var audienceOnly = new LearningRuleSettings { Name = LearningRuleNames.People, Values = { ["Audience"] = "field-staff" } };

        // Act
        var none = await rule.EvaluateAsync(Context(empty, "ana"));
        var noFeature = await rule.EvaluateAsync(Context(audienceOnly, "ana"));

        // Assert
        Assert.False(none.IsReleased);
        Assert.False(noFeature.IsReleased);
    }

    [Fact]
    public void ThePeopleRule_IsRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        CrestApps.OrchardCore.Learning.Core.LearningCoreServiceCollectionExtensions.AddLearningCore(services);

        // Act
        var registered = services.Any(descriptor => descriptor.ServiceType == typeof(ICourseItemReleaseRule) && descriptor.ImplementationType == typeof(PeopleReleaseRule));

        // Assert
        Assert.True(registered);
    }

    [Fact]
    public void PathRules_LeaveOutCoursesThatAreNotAvailable()
    {
        // Arrange
        var path = new LearningPathPart
        {
            Sets =
            [
                new LearningPathCourseSet { Id = "s1", CourseIds = ["c1", "hidden"] },
                new LearningPathCourseSet { Id = "s2", Connector = LearningPathConnector.Then, CourseIds = ["hidden2"] },
                new LearningPathCourseSet { Id = "s3", Connector = LearningPathConnector.Then, Join = LearningPathSetJoin.AtLeast, MinimumCount = 2, CourseIds = ["c2", "c3", "hidden3"] },
            ],
        };
        var available = new HashSet<string>(StringComparer.Ordinal) { "c1", "c2", "c3" };

        // Act
        var visible = LearningPathRules.WithAvailableCourses(path, available);

        // Assert
        Assert.Equal(["c1"], visible.Sets[0].CourseIds);
        Assert.Empty(visible.Sets[1].CourseIds);
        Assert.Equal(["c2", "c3"], visible.Sets[2].CourseIds);
        Assert.Equal(2, LearningPathRules.OpenStage(visible, new HashSet<string>(StringComparer.Ordinal) { "c1" }));
        Assert.Equal(3, LearningPathRules.OpenStage(visible, new HashSet<string>(StringComparer.Ordinal) { "c1", "c2", "c3" }));
    }

    [Fact]
    public async Task APath_SkipsItsUnpublishedCourses_AndCompletesWithoutThem()
    {
        // Arrange
        var h = await ProgramsHarness.CreateAsync();
        h.Inner.Courses.AddCourse("hidden");
        h.Inner.Courses.AddItem("hidden", "hidden-lesson");
        h.Inner.Courses.Get("hidden").Published = false;
        h.AddPath("plan",
            new LearningPathCourseSet { Id = "s1", CourseIds = ["c1", "hidden"] },
            new LearningPathCourseSet { Id = "s2", Connector = LearningPathConnector.Then, CourseIds = ["c2"] });

        // Act
        var started = await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().StartAsync(new LearningPathStartRequest { PathId = "plan", UserId = "ana", Actor = "admin" }));
        var hiddenEnrollments = await h.EnrollmentsAsync("ana", "hidden");
        await h.CompleteCourseAsync("ana", "c1");
        var afterFirst = await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().FindProgressAsync("plan", "ana"));
        await h.CompleteCourseAsync("ana", "c2");
        var done = await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().FindProgressAsync("plan", "ana"));

        // Assert
        Assert.True(started.Created, started.Error);
        Assert.Empty(hiddenEnrollments);
        Assert.Equal(1, afterFirst.OpenedStage);
        Assert.Equal(LearningPathState.Completed, done.State);
    }

    private static ReleaseRuleContext Context(LearningRuleSettings settings, string userId)
        => new()
        {
            Settings = settings,
            Enrollment = new Enrollment { UserId = userId },
            Progress = new EnrollmentProgress(),
            UtcNow = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc),
        };
}
