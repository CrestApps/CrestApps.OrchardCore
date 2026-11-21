using System.Net;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The course editor: the blank "New section" row it always shows is not saved as a section, and the course setting
/// "Learners take the items in order" is saved.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class CourseOutlineEditorTests
{
    private readonly LearningSiteFixture _fixture;

    public CourseOutlineEditorTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ABlankSectionRow_IsNotSaved_AndItemsInOrderIsKept()
    {
        // Arrange
        using var admin = await _fixture.CreateClientAsync("admin");
        var form = await admin.GetAsync("Admin/Contents/ContentTypes/Course/Create", TestContext.Current.CancellationToken);
        var token = LearningSiteFixture.Token(await form.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var title = "Outline editor " + Guid.NewGuid().ToString("n")[..6];

        // Act: one named section and the blank row the editor shows for a new one.
        var response = await admin.PostAsync("Admin/Contents/ContentTypes/Course/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["TitlePart.Title"] = title,
            ["CoursePart.EnrollmentMode"] = "Open",
            ["CoursePart.ItemsInOrder"] = "true",
            ["CourseOutlinePart.Sections[0].Id"] = "named",
            ["CourseOutlinePart.Sections[0].Title"] = "Getting started",
            ["CourseOutlinePart.Sections[0].Order"] = "1",
            ["CourseOutlinePart.Sections[1].Id"] = "blankrow",
            ["CourseOutlinePart.Sections[1].Title"] = "  ",
            ["CourseOutlinePart.Sections[1].Order"] = "2",
            ["submit.Save"] = "submit.Save",
            ["__RequestVerificationToken"] = token,
        }), TestContext.Current.CancellationToken);

        var course = await _fixture.InTenantAsync(async services =>
        {
            var session = services.GetRequiredService<global::YesSql.ISession>();
            var items = await session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == LearningConstants.ContentTypes.Course && index.Latest && index.DisplayText == title).ListAsync();

            return items.Single();
        });

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(["Getting started"], course.Get<CourseOutlinePart>(nameof(CourseOutlinePart)).Sections.Select(section => section.Title));
        Assert.True(course.Get<CoursePart>(nameof(CoursePart)).ItemsInOrder);
    }
}
