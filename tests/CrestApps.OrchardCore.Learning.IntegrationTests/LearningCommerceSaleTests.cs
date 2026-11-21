using System.Net;
using System.Reflection;
using CrestApps.OrchardCore.Learning.Programs;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Title.Models;
using YesSql;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// What Learning Commerce puts up for sale: a priced learning path is sold on its page, and a course without a price
/// saves and is simply not for sale.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LearningCommerceSaleTests
{
    private readonly LearningSiteFixture _fixture;

    public LearningCommerceSaleTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task APricedLearningPath_ShowsItsPriceAndAddToCart()
    {
        // Arrange
        var pathId = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var path = await contentManager.NewAsync(ProgramsConstants.LearningPathContentType);

            path.DisplayText = "Sold path";
            path.Alter<TitlePart>(part => part.Title = "Sold path");
            path.Alter<LearningPathPart>(part => part.Sets = [new LearningPathCourseSet { Id = "s1", Title = "Stage 1", CourseIds = [_fixture.CourseId] }]);
            path.Alter<ProductPart>(part =>
            {
                part.Price = 15;
                part.Currency = "USD";
            });
            await contentManager.CreateAsync(path, VersionOptions.Published);

            return path.ContentItemId;
        });
        using var learner = await _fixture.CreateClientAsync("learner2");

        // Act
        var response = await learner.GetAsync($"Contents/ContentItems/{pathId}", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(AddToCartOf(pathId), html);
    }

    [Fact]
    public async Task ALearningPathTypeWithoutTheProductDetails_GetsThemFromTheUpgradeStep()
    {
        // Arrange: the browser pass site's Learning Path type had the product part without the page details.
        var pathId = await _fixture.InTenantAsync(async services =>
        {
            await services.GetRequiredService<IContentDefinitionManager>().AlterTypeDefinitionAsync(ProgramsConstants.LearningPathContentType, type => type
                .WithPart(nameof(ProductPart), part => part.MergeSettings<ProductPageSettings>(settings => settings.ShowDetails = false)));

            var contentManager = services.GetRequiredService<IContentManager>();
            var path = await contentManager.NewAsync(ProgramsConstants.LearningPathContentType);

            path.DisplayText = "Older site path";
            path.Alter<TitlePart>(part => part.Title = "Older site path");
            path.Alter<ProductPart>(part =>
            {
                part.Price = 20;
                part.Currency = "USD";
            });
            await contentManager.CreateAsync(path, VersionOptions.Published);

            return path.ContentItemId;
        });
        using var learner = await _fixture.CreateClientAsync("learner2");
        var before = await (await learner.GetAsync($"Contents/ContentItems/{pathId}", TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Act: run the commerce migration's upgrade step as a site made before it does at its next start.
        await _fixture.InTenantAsync(async services =>
        {
            var migrationType = Assembly.Load("CrestApps.OrchardCore.Learning.Commerce").GetType("CrestApps.OrchardCore.Learning.Commerce.Migrations.LearningCommerceMigrations", throwOnError: true);
            var migration = ActivatorUtilities.CreateInstance(services, migrationType);
            var step = migrationType.GetMethod("UpdateFrom2Async");

            Assert.NotNull(step);

            return await (Task<int>)step.Invoke(migration, null);
        });
        var after = await (await learner.GetAsync($"Contents/ContentItems/{pathId}", TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(AddToCartOf(pathId), before);
        Assert.Contains(AddToCartOf(pathId), after);
    }

    [Fact]
    public async Task ACourseWithoutAPrice_SavesAndIsNotForSale()
    {
        // Arrange
        var title = "Free course " + Guid.NewGuid().ToString("N");
        using var admin = await _fixture.CreateClientAsync("admin");
        var form = await admin.GetAsync("Admin/Contents/ContentTypes/Course/Create", TestContext.Current.CancellationToken);
        var token = LearningSiteFixture.Token(await form.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // Act
        var response = await admin.PostAsync("Admin/Contents/ContentTypes/Course/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["TitlePart.Title"] = title,
            ["CoursePart.EnrollmentMode"] = "Open",
            ["ProductPart.Price"] = string.Empty,
            ["submit.Publish"] = "submit.Publish",
            ["__RequestVerificationToken"] = token,
        }), TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var courseId = await _fixture.InTenantAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            var item = await session.Query<ContentItem, ContentItemIndex>(index => index.DisplayText == title && index.Published).FirstOrDefaultAsync();

            return item?.ContentItemId;
        });

        // Assert
        Assert.DoesNotContain("Price is required", html);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(courseId);

        using var learner = await _fixture.CreateClientAsync("learner2");
        var page = await (await learner.GetAsync($"Contents/ContentItems/{courseId}", TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(AddToCartOf(courseId), page);
    }

    // The Add to cart form of an item carries its id in a hidden field.
    private static string AddToCartOf(string contentItemId)
        => $"name=\"contentItemId\" value=\"{contentItemId}\"";
}
