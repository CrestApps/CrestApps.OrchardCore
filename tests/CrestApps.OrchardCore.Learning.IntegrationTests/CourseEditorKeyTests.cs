using System.Net;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// A course create that fails in a part after the Course part saves nothing, so its form does not claim an enrollment
/// key is set.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class CourseEditorKeyTests
{
    private readonly LearningSiteFixture _fixture;

    public CourseEditorKeyTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AFailedCreate_OfAKeyCourse_DoesNotSayAKeyIsSet()
    {
        // Arrange
        using var admin = await _fixture.CreateClientAsync("admin");
        var form = await admin.GetAsync("Admin/Contents/ContentTypes/Course/Create", TestContext.Current.CancellationToken);
        var token = LearningSiteFixture.Token(await form.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // Act: the Course part is valid; the outline part after it is not (its order is not a number).
        var response = await admin.PostAsync("Admin/Contents/ContentTypes/Course/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["TitlePart.Title"] = "Key course that fails",
            ["CoursePart.EnrollmentMode"] = "Key",
            ["CoursePart.NewEnrollmentKey"] = "alpha123",
            ["CourseOutlinePart.Items[0].ContentItemId"] = "4nosuchitem00000000000000",
            ["CourseOutlinePart.Items[0].Order"] = "not a number",
            ["submit.Publish"] = "submit.Publish",
            ["__RequestVerificationToken"] = token,
        }), TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("CoursePart.NewEnrollmentKey", html);
        Assert.DoesNotContain("A key is set.", html);
    }
}
