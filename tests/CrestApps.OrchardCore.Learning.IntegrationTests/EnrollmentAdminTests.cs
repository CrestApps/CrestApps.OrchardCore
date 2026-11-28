using System.Net;
using CrestApps.OrchardCore.Learning.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The enrollment admin pages as an instructor sees them: only their own courses, and only people who exist.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class EnrollmentAdminTests
{
    private readonly LearningSiteFixture _fixture;

    public EnrollmentAdminTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AnInstructor_EnrollsALearnerIntoTheirOwnCourse_Only()
    {
        // Arrange
        using var instructor = await _fixture.CreateClientAsync("instructor1");
        var form = await instructor.GetAsync("Admin/learning/enrollments/new", TestContext.Current.CancellationToken);
        var html = await form.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var token = LearningSiteFixture.Token(html);

        Task<HttpResponseMessage> Enroll(string userId, string courseId)
            => instructor.PostAsync("Admin/learning/enrollments/new", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["UserId"] = userId,
                ["CourseId"] = courseId,
                ["__RequestVerificationToken"] = token,
            }), TestContext.Current.CancellationToken);

        // Other tests of the shared site enroll the spare learner too (required learning), so only the enrollments this
        // test makes are checked.
        var before = (await _fixture.InTenantAsync(async services => await services.GetRequiredService<EnrollmentStore>().ListByUserAsync(_fixture.SpareLearnerId)))
            .Select(enrollment => enrollment.ItemId)
            .ToHashSet(StringComparer.Ordinal);

        // Act
        var unknown = await Enroll("4nosuchuser000000000000000", _fixture.CourseId);
        var other = await Enroll(_fixture.SpareLearnerId, _fixture.OtherCourseId);
        var own = await Enroll(_fixture.SpareLearnerId, _fixture.CourseId);
        var list = await (await instructor.GetAsync("Admin/learning/enrollments", TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        Assert.Contains(_fixture.CourseId, html);
        Assert.DoesNotContain(_fixture.OtherCourseId, html);

        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Contains("The learner does not exist.", await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        // Forbid sends a signed-in person to the access denied page; a success sends them to the new enrollment.
        Assert.DoesNotContain("learning/enrollments", other.Headers.Location?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Redirect, own.StatusCode);
        Assert.Contains("learning/enrollments/", own.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_fixture.OtherCourseId, list);

        var enrollments = (await _fixture.InTenantAsync(async services => await services.GetRequiredService<EnrollmentStore>().ListByUserAsync(_fixture.SpareLearnerId)))
            .Where(enrollment => !before.Contains(enrollment.ItemId));
        var enrollment = Assert.Single(enrollments);
        Assert.Equal(_fixture.CourseId, enrollment.CourseId);
    }

    [Fact]
    public async Task AnInstructor_IsOfferedTheEnrollALearnerButton()
    {
        // Arrange
        using var instructor = await _fixture.CreateClientAsync("instructor1");

        // Act
        var response = await instructor.GetAsync("Admin/learning/enrollments", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Enroll a Learner", html);
    }
}
