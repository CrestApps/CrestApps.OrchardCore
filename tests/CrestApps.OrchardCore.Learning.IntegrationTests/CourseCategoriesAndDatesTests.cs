using System.Net;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Settings;
using OrchardCore.Taxonomies.Settings;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Course categories and course dates on a running site: the feature's taxonomy, category field and admin list filter,
/// the catalog's category browsing with sub-categories, and the dates in the catalog and on the course page. The site is
/// shared, so each test makes its own courses and categories with names of its own.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class CourseCategoriesAndDatesTests
{
    private readonly LearningSiteFixture _fixture;

    public CourseCategoriesAndDatesTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TheFeature_GivesCoursesACategoryField_ATaxonomy_AndAFilterInTheAdminList()
    {
        // Act
        var (fieldName, taxonomyId, taxonomyTitle, filters) = await _fixture.InTenantAsync(async services =>
        {
            var type = await services.GetRequiredService<IContentDefinitionManager>().GetTypeDefinitionAsync(LearningConstants.ContentTypes.Course);
            var field = type.Parts
                .Single(part => part.Name == LearningConstants.ContentTypes.Course)
                .PartDefinition.Fields
                .Single(candidate => candidate.FieldDefinition.Name == "TaxonomyField");
            var taxonomyId = field.GetSettings<TaxonomyFieldSettings>().TaxonomyContentItemId;
            var taxonomy = await services.GetRequiredService<IContentManager>().GetAsync(taxonomyId, VersionOptions.Published);
            var settings = await services.GetRequiredService<ISiteService>().GetSettingsAsync<TaxonomyContentsAdminListSettings>();

            return (field.Name, taxonomyId, taxonomy?.DisplayText, settings.TaxonomyContentItemIds);
        });

        // Assert
        Assert.Equal(LearningConstants.CourseCategories.FieldName, fieldName);
        Assert.False(string.IsNullOrEmpty(taxonomyId));
        Assert.Equal(LearningConstants.CourseCategories.TaxonomyName, taxonomyTitle);
        Assert.Contains(taxonomyId, filters);
    }

    [Fact]
    public async Task TheCatalog_BrowsesACategoryWithItsSubCategories_AndShowsTheDates()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("n")[..8];
        var (parentId, childId, otherId) = await _fixture.InTenantAsync(async services =>
        {
            var categories = services.GetRequiredService<CourseCategoryService>();
            var parent = await categories.FindOrAddAsync(["Catalog parent " + suffix]);
            var child = await categories.FindOrAddAsync(["Catalog parent " + suffix, "Catalog child " + suffix]);
            var other = await categories.FindOrAddAsync(["Catalog other " + suffix]);

            return (parent, child, other);
        });

        var inChild = await CreateCourseAsync("Course in the child " + suffix, childId, starts: new DateTime(2031, 3, 1, 15, 0, 0, DateTimeKind.Utc), ends: new DateTime(2031, 6, 30, 15, 0, 0, DateTimeKind.Utc));
        var inParent = await CreateCourseAsync("Course in the parent " + suffix, parentId);
        var inOther = await CreateCourseAsync("Course elsewhere " + suffix, otherId);
        using var anonymous = await _fixture.CreateClientAsync();

        // Act
        var all = await anonymous.GetStringAsync("learning/catalog?q=" + suffix, TestContext.Current.CancellationToken);
        var parent = await anonymous.GetStringAsync("learning/catalog?category=" + parentId, TestContext.Current.CancellationToken);
        var child = await anonymous.GetStringAsync("learning/catalog?category=" + childId, TestContext.Current.CancellationToken);

        // Assert: browsing the parent lists the courses of its sub-category too; the catalog names each course's
        // category and shows the dates in the site's time zone (never as UTC).
        Assert.Contains("Course in the child " + suffix, all, StringComparison.Ordinal);
        Assert.Contains("Course elsewhere " + suffix, all, StringComparison.Ordinal);
        Assert.Contains("Catalog parent " + suffix, all, StringComparison.Ordinal);

        Assert.Contains("Course in the child " + suffix, parent, StringComparison.Ordinal);
        Assert.Contains("Course in the parent " + suffix, parent, StringComparison.Ordinal);
        Assert.DoesNotContain("Course elsewhere " + suffix, parent, StringComparison.Ordinal);

        Assert.Contains("Course in the child " + suffix, child, StringComparison.Ordinal);
        Assert.DoesNotContain("Course in the parent " + suffix, child, StringComparison.Ordinal);
        Assert.Contains("data-course-starts", child, StringComparison.Ordinal);
        Assert.Contains("data-course-ends", child, StringComparison.Ordinal);
        Assert.Contains("2031", child, StringComparison.Ordinal);
        Assert.DoesNotContain("UTC", child, StringComparison.Ordinal);
        Assert.NotNull(inParent);
        Assert.NotNull(inOther);

        // The course page shows the dates too.
        var page = await anonymous.GetAsync(inChild.Path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("data-course-starts", html, StringComparison.Ordinal);
        Assert.Contains("data-course-ends", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheAdminListOfCourses_FiltersByCategory()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("n")[..8];
        var (taxonomyId, termId, otherTermId) = await _fixture.InTenantAsync(async services =>
        {
            var categories = services.GetRequiredService<CourseCategoryService>();

            return (await categories.GetTaxonomyIdAsync(), await categories.FindOrAddAsync(["Admin filter " + suffix]), await categories.FindOrAddAsync(["Admin other " + suffix]));
        });

        await CreateCourseAsync("Filtered course " + suffix, termId);
        await CreateCourseAsync("Unfiltered course " + suffix, otherTermId);
        using var admin = await _fixture.CreateClientAsync("admin");

        // Act
        var list = await admin.GetStringAsync(
            $"Admin/Contents/ContentItems/{LearningConstants.ContentTypes.Course}?Taxonomy{taxonomyId}.SelectedContentItemId=Term:{termId}",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Filtered course " + suffix, list, StringComparison.Ordinal);
        Assert.DoesNotContain("Unfiltered course " + suffix, list, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACourseThatHasNotStarted_ShowsWhenItOpens_InsteadOfItsItems()
    {
        // Arrange: the fixture's learner is enrolled in the fixture's course, whose start moves a week ahead.
        var original = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await contentManager.GetAsync(_fixture.CourseId, VersionOptions.Published);
            var starts = course.Get<CoursePart>(nameof(CoursePart)).StartsUtc;
            course.Alter<CoursePart>(part => part.StartsUtc = DateTime.UtcNow.AddDays(7));
            await contentManager.UpdateAsync(course);

            return starts;
        });

        try
        {
            using var learner = await _fixture.CreateClientAsync("learner1");

            // Act
            var player = await learner.GetStringAsync($"learning/courses/{_fixture.CourseId}/play/{_fixture.PageLessonId}", TestContext.Current.CancellationToken);

            // Assert
            Assert.DoesNotContain(LearningSiteFixture.SecretLessonText, player, StringComparison.Ordinal);
            Assert.Contains("The course has not started yet.", player, StringComparison.Ordinal);
            Assert.Contains("It opens on", player, StringComparison.Ordinal);
        }
        finally
        {
            await _fixture.InTenantAsync(async services =>
            {
                var contentManager = services.GetRequiredService<IContentManager>();
                var course = await contentManager.GetAsync(_fixture.CourseId, VersionOptions.Published);
                course.Alter<CoursePart>(part => part.StartsUtc = original);
                await contentManager.UpdateAsync(course);
            });
        }
    }

    private Task<(string Id, string Path)> CreateCourseAsync(string title, string categoryId, DateTime? starts = null, DateTime? ends = null)
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);
            course.Alter<TitlePart>(part => part.Title = title);
            course.Alter<CoursePart>(part =>
            {
                part.Summary = "Summary of " + title;
                part.StartsUtc = starts;
                part.EndsUtc = ends;
            });
            await services.GetRequiredService<CourseCategoryService>().SetCategoriesAsync(course, [categoryId]);
            await contentManager.CreateAsync(course, VersionOptions.Published);
            var path = course.Get<global::OrchardCore.Autoroute.Models.AutoroutePart>("AutoroutePart")?.Path;

            return (course.ContentItemId, "/" + path);
        });
}
