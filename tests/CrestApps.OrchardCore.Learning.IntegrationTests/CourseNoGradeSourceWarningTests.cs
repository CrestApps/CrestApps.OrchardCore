using System.Net;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Html.Models;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// R10-Mi4: the course editor decides "nothing gives a grade" by the same rule as the course grade
/// (<c>CourseGradeCalculator.HasNoGradeSource</c>), so a course whose graded items all sit in a gradebook category that
/// weighs nothing gets the warning under the minimum course grade.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class CourseNoGradeSourceWarningTests
{
    private const string Warning = "Nothing in this course gives a grade";

    private readonly LearningSiteFixture _fixture;

    public CourseNoGradeSourceWarningTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(40, false)]
    public async Task TheCourseEditor_WarnsWhenEveryGradedItemSitsInACategoryThatWeighsNothing(int categoryWeight, bool warns)
    {
        // Arrange: a course with a minimum course grade, one gradebook category, and a gradable lesson with a weight in it.
        var suffix = Guid.NewGuid().ToString("n")[..6];
        var courseId = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);

            course.DisplayText = "Weighted course " + suffix;
            course.Alter<TitlePart>(part => part.Title = course.DisplayText);
            course.Alter<CoursePart>(part =>
            {
                part.EnrollmentMode = EnrollmentMode.Open;
                part.GradeCategories = [new GradeCategory { Id = "c" + suffix, Name = "Category " + suffix, Weight = categoryWeight }];
                part.CompletionCriteria = [new LearningRuleSettings { Name = LearningRuleNames.MinimumGrade, Values = { ["Grade"] = "70" } }];
            });
            await contentManager.CreateAsync(course, VersionOptions.Draft);

            var lesson = await contentManager.NewAsync(LearningConstants.ContentTypes.PageLesson);

            lesson.DisplayText = "Graded work " + suffix;
            lesson.Alter<TitlePart>(part => part.Title = lesson.DisplayText);
            lesson.Alter<HtmlBodyPart>(part => part.Html = "<p>Do it.</p>");
            lesson.Alter<ContainedPart>(part =>
            {
                part.ListContentItemId = course.ContentItemId;
                part.ListContentType = course.ContentType;
            });
            lesson.Alter<CourseItemPart>(part =>
            {
                part.CompletionMode = CompletionModes.Viewed;
                part.Gradable = true;
                part.Weight = 1;
                part.GradeCategoryId = "c" + suffix;
            });
            await contentManager.CreateAsync(lesson, VersionOptions.Published);

            course.Alter<CourseOutlinePart>(part => part.Sections =
            [
                new CourseSection { Id = "s1", Title = "Work", Items = [new CourseOutlineItem { ContentItemId = lesson.ContentItemId }] },
            ]);
            await contentManager.UpdateAsync(course);
            await contentManager.PublishAsync(course);

            return course.ContentItemId;
        });

        using var admin = await _fixture.CreateClientAsync("admin");

        // Act
        var editor = await admin.GetAsync($"Admin/Contents/ContentItems/{courseId}/Edit", TestContext.Current.CancellationToken);
        var page = await editor.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert: the warning shows only when the category weighs nothing, as the course grade then is empty.
        Assert.Equal(HttpStatusCode.OK, editor.StatusCode);
        Assert.Equal(warns, page.Contains(Warning, StringComparison.Ordinal));
    }
}
