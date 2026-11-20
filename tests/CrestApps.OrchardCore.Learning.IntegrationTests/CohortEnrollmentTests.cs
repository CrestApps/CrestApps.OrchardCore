using System.Net;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Programs;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Learning section 3, item 5: cohorts. A course or a learning path linked to cohorts enrolls the people in them,
/// enrolls the people who join later, and ends only what the sync made for the people who leave: an enrollment another
/// source also grants, a completed one, and a path someone else started all stay.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class CohortEnrollmentTests
{
    private readonly LearningSiteFixture _fixture;

    public CohortEnrollmentTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ACourseLinkedToACohort_EnrollsItsMembers_AndTheOnesWhoJoinLater_AndLeavingEndsOnlyTheCohortGrant()
    {
        // Arrange: a cohort with one person, and a course linked to it; another person is enrolled by hand.
        var (ana, ben, cid) = await UsersAsync();
        var cohortId = await CohortAsync(ana);
        var courseId = await CourseAsync(cohortId);

        await _fixture.InTenantAsync(async services =>
        {
            var result = await services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
            {
                UserId = ben,
                CourseId = courseId,
                Source = LearningConstants.EnrollmentSources.Manual,
                SourceId = "by-hand",
            });

            Assert.True(result.Succeeded, result.ErrorCode);
        });

        var anaAtLink = await OpenAsync(ana, courseId);

        // Act: two people join, then both leave.
        await _fixture.InTenantAsync(services => services.GetRequiredService<ICohortService>().AddMembersAsync(cohortId, [ben, cid], "admin"));

        var benJoined = await OpenAsync(ben, courseId);
        var cidJoined = await OpenAsync(cid, courseId);

        await _fixture.InTenantAsync(services => services.GetRequiredService<ICohortService>().RemoveMembersAsync(cohortId, [ben, cid], "admin"));

        var benLeft = await LatestAsync(ben, courseId);
        var cidLeft = await LatestAsync(cid, courseId);

        // Assert
        Assert.NotNull(anaAtLink);
        Assert.Contains(anaAtLink.Grants, grant => grant.Source == LearningConstants.EnrollmentSources.Cohort);
        Assert.Equal(2, benJoined.Grants.Count);
        Assert.Contains(cidJoined.Grants, grant => grant.Source == LearningConstants.EnrollmentSources.Cohort);

        Assert.Equal(EnrollmentStatus.Active, benLeft.Status);
        var grant = Assert.Single(benLeft.Grants);
        Assert.Equal(LearningConstants.EnrollmentSources.Manual, grant.Source);
        Assert.Equal(EnrollmentStatus.Withdrawn, cidLeft.Status);
    }

    [Fact]
    public async Task UnlinkingACohort_EndsItsOpenEnrollments_ButACompletedOneStays_AndLinkingAgainNeverRetakesIt()
    {
        // Arrange: two members enrolled through the cohort; one completes the course.
        var (ana, ben, _) = await UsersAsync();
        var cohortId = await CohortAsync(ana, ben);
        var courseId = await CourseAsync(cohortId);
        var anaEnrollment = await OpenAsync(ana, courseId);

        await _fixture.InTenantAsync(async services =>
        {
            var result = await services.GetRequiredService<IEnrollmentService>().ChangeStatusAsync(anaEnrollment.ItemId, EnrollmentStatus.Completed, "admin", "Finished in class");

            Assert.True(result.Succeeded, result.ErrorCode);
        });

        // Act: the cohort is unlinked, then linked again.
        await LinkAsync(courseId);

        var anaUnlinked = await LatestAsync(ana, courseId);
        var benUnlinked = await LatestAsync(ben, courseId);

        await LinkAsync(courseId, cohortId);

        var anaEnrollments = await AllAsync(ana, courseId);

        // Assert
        Assert.Equal(EnrollmentStatus.Completed, anaUnlinked.Status);
        Assert.Equal(EnrollmentStatus.Withdrawn, benUnlinked.Status);
        Assert.Equal(anaEnrollment.ItemId, Assert.Single(anaEnrollments).ItemId);
        Assert.NotNull(await OpenAsync(ben, courseId));
    }

    [Fact]
    public async Task ALearningPathLinkedToCohorts_StartsTheirMembers_AndLeavingTakesThemOff_OnlyWhenNoLinkedCohortHoldsThemAndTheSyncStartedIt()
    {
        // Arrange: a path with one course, linked to two cohorts; ana is in both, ben in the first; cid started the
        // path himself before joining.
        var (ana, ben, cid) = await UsersAsync();
        var first = await CohortAsync(ana, ben);
        var second = await CohortAsync(ana);
        var courseId = await CourseAsync();
        var pathId = await PathAsync(courseId);

        await _fixture.InTenantAsync(async services =>
        {
            var started = await services.GetRequiredService<ILearningPathService>().StartAsync(new LearningPathStartRequest
            {
                PathId = pathId,
                UserId = cid,
                Source = "Self",
                Actor = cid,
            });

            Assert.NotNull(started.Progress);
        });

        await LinkAsync(pathId, first, second);
        await _fixture.InTenantAsync(services => services.GetRequiredService<ICohortService>().AddMembersAsync(first, [cid], "admin"));

        var anaStarted = await ProgressAsync(pathId, ana);
        var benStarted = await ProgressAsync(pathId, ben);
        var benCourse = await OpenAsync(ben, courseId);

        // Act: everyone leaves the first cohort.
        await _fixture.InTenantAsync(services => services.GetRequiredService<ICohortService>().RemoveMembersAsync(first, [ana, ben, cid], "admin"));

        // Assert
        Assert.Equal(LearningConstants.EnrollmentSources.Cohort, anaStarted?.Source);
        Assert.Equal(LearningConstants.EnrollmentSources.Cohort, benStarted?.Source);
        Assert.Contains(benCourse.Grants, grant => grant.Source == ProgramsConstants.PathSource);

        Assert.NotNull(await ProgressAsync(pathId, ana));
        Assert.Null(await ProgressAsync(pathId, ben));
        Assert.Equal(EnrollmentStatus.Withdrawn, (await LatestAsync(ben, courseId)).Status);
        Assert.Equal("Self", (await ProgressAsync(pathId, cid))?.Source);
    }

    [Fact]
    public async Task ACohortLinkedToACourse_CannotBeDeleted_AndTheAdminShowsTheCohortsAndTheirLinks()
    {
        // Arrange
        var (ana, _, _) = await UsersAsync();
        var cohortId = await CohortAsync(ana);
        var courseId = await CourseAsync(cohortId);

        // Act
        var refused = await _fixture.InTenantAsync(services => services.GetRequiredService<ICohortService>().DeleteAsync(cohortId));

        using var admin = await _fixture.CreateClientAsync("admin");
        var list = await admin.GetAsync("Admin/cohorts", TestContext.Current.CancellationToken);
        var members = await admin.GetStringAsync($"Admin/cohorts/members/{cohortId}", TestContext.Current.CancellationToken);
        var edit = await admin.GetAsync($"Admin/cohorts/edit/{cohortId}", TestContext.Current.CancellationToken);
        var editor = await admin.GetStringAsync($"Admin/Contents/ContentItems/{courseId}/Edit", TestContext.Current.CancellationToken);

        using var learner = await _fixture.CreateClientAsync("learner2");
        var forbidden = await learner.GetAsync("Admin/cohorts", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(refused);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains("ana", members, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Contains("learning-cohorts-card", editor, StringComparison.Ordinal);
        Assert.Contains(cohortId, editor, StringComparison.Ordinal);
        Assert.NotEqual(HttpStatusCode.OK, forbidden.StatusCode);
    }

    private async Task<(string Ana, string Ben, string Cid)> UsersAsync()
    {
        var suffix = Guid.NewGuid().ToString("n")[..6];

        return (
            await _fixture.InTenantAsync(services => LearningSiteFixture.CreateUserAsync(services, "ana" + suffix)),
            await _fixture.InTenantAsync(services => LearningSiteFixture.CreateUserAsync(services, "ben" + suffix)),
            await _fixture.InTenantAsync(services => LearningSiteFixture.CreateUserAsync(services, "cid" + suffix)));
    }

    private Task<string> CohortAsync(params string[] userIds)
        => _fixture.InTenantAsync(async services =>
        {
            var cohorts = services.GetRequiredService<ICohortService>();
            var result = await cohorts.CreateAsync("Cohort " + Guid.NewGuid().ToString("n")[..8], null);

            Assert.True(result.Succeeded, string.Join(", ", result.Errors));
            await cohorts.AddMembersAsync(result.Cohort.CohortId, userIds, "admin");

            return result.Cohort.CohortId;
        });

    private Task<string> CourseAsync(params string[] cohortIds)
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);

            course.DisplayText = "Cohort course " + Guid.NewGuid().ToString("n")[..6];
            course.Alter<TitlePart>(part => part.Title = course.DisplayText);
            course.Alter<LearningCohortsPart>(part => part.CohortIds = [.. cohortIds]);
            await contentManager.CreateAsync(course, VersionOptions.Published);

            return course.ContentItemId;
        });

    private Task<string> PathAsync(string courseId)
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var path = await contentManager.NewAsync(ProgramsConstants.LearningPathContentType);

            path.DisplayText = "Cohort path " + Guid.NewGuid().ToString("n")[..6];
            path.Alter<TitlePart>(part => part.Title = path.DisplayText);
            path.Alter<LearningPathPart>(part => part.Sets = [new LearningPathCourseSet { Id = "s1", CourseIds = [courseId] }]);
            await contentManager.CreateAsync(path, VersionOptions.Published);

            return path.ContentItemId;
        });

    private Task LinkAsync(string contentItemId, params string[] cohortIds)
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var item = await contentManager.GetAsync(contentItemId, VersionOptions.DraftRequired);

            item.Alter<LearningCohortsPart>(part => part.CohortIds = [.. cohortIds]);
            await contentManager.UpdateAsync(item);
            await contentManager.PublishAsync(item);
        });

    private Task<IReadOnlyList<Enrollment>> AllAsync(string userId, string courseId)
        => _fixture.InTenantAsync(services => services.GetRequiredService<IEnrollmentStore>().ListByCourseForUserAsync(userId, courseId));

    private async Task<Enrollment> OpenAsync(string userId, string courseId)
        => (await AllAsync(userId, courseId)).FirstOrDefault(enrollment => enrollment.IsOpen);

    private async Task<Enrollment> LatestAsync(string userId, string courseId)
        => (await AllAsync(userId, courseId)).OrderByDescending(enrollment => enrollment.CreatedUtc).ThenByDescending(enrollment => enrollment.Cycle).First();

    private Task<LearningPathProgress> ProgressAsync(string pathId, string userId)
        => _fixture.InTenantAsync(services => services.GetRequiredService<ILearningPathService>().FindProgressAsync(pathId, userId));
}
