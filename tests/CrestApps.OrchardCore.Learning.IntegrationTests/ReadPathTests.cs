using System.Net;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The read paths that do not ask for ViewContent on each item: the List part feed, search results and the queries API.
/// None of them may hand a lesson's body to someone who is not enrolled.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class ReadPathTests
{
    private readonly LearningSiteFixture _fixture;

    public ReadPathTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TheCourseFeed_DoesNotCarryLessonBodies()
    {
        // Arrange
        using var anonymous = await _fixture.CreateClientAsync();

        // Act: the List part offers an RSS feed of every list, the course included, unless it is turned off.
        var feed = await anonymous.GetAsync($"OrchardCore.Feeds/Feed/Index?format=rss&contentItemId={_fixture.CourseId}", TestContext.Current.CancellationToken);
        var body = await feed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(LearningSiteFixture.SecretLessonText, body);
    }

    [Fact]
    public async Task SearchResults_OfAnIndexThatHoldsLessons_ShowNoLessonBody()
    {
        // Arrange: a site that adds lessons to a search index anyone may query.
        await _fixture.InTenantAsync(services => LearningSiteFixture.RunRecipeAsync(services, new JsonObject
        {
            ["name"] = "LearningLessonSearch",
            ["steps"] = new JsonArray(
                new JsonObject
                {
                    ["name"] = "CreateOrUpdateIndexProfile",
                    ["indexes"] = new JsonArray(new JsonObject
                    {
                        ["IndexName"] = "lessons",
                        ["ProviderName"] = "Lucene",
                        ["Type"] = "Content",
                        ["Name"] = "lessons",
                        ["Properties"] = new JsonObject
                        {
                            ["LuceneIndexMetadata"] = new JsonObject { ["StoreSourceData"] = false, ["AnalyzerName"] = "standardanalyzer" },
                            ["ContentIndexMetadata"] = new JsonObject { ["IndexLatest"] = false, ["IndexedContentTypes"] = new JsonArray(LearningConstants.ContentTypes.PageLesson), ["Culture"] = "any" },
                            ["LuceneIndexDefaultQueryMetadata"] = new JsonObject
                            {
                                ["DefaultVersion"] = "LUCENE_30",
                                ["QueryAnalyzerName"] = "standardanalyzer",
                                ["AllowLuceneQueries"] = false,
                                ["DefaultSearchFields"] = new JsonArray("Content.ContentItem.FullText"),
                            },
                        },
                        ["IndexLatest"] = false,
                        ["Culture"] = "any",
                        ["IndexedContentTypes"] = new JsonArray(LearningConstants.ContentTypes.PageLesson),
                    }),
                },
                new JsonObject { ["name"] = "RebuildIndex", ["includeAll"] = false, ["indexNames"] = new JsonArray("lessons") },
                new JsonObject
                {
                    ["name"] = "Roles",
                    ["Roles"] = new JsonArray(new JsonObject
                    {
                        ["Name"] = "Anonymous",
                        ["Description"] = "Anonymous role",
                        ["PermissionBehavior"] = "Add",
                        ["Permissions"] = new JsonArray("QueryIndex_lessons"),
                    }),
                }),
        }));

        using var anonymous = await _fixture.CreateClientAsync();

        // Act: the index is built in the background, so wait until the lesson is found.
        HttpResponseMessage page = null;
        var html = string.Empty;

        for (var attempt = 0; attempt < 60 && !html.Contains("Fire exits", StringComparison.Ordinal); attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(1000, TestContext.Current.CancellationToken);
            }

            page = await anonymous.GetAsync("search/lessons?Terms=lift", TestContext.Current.CancellationToken);
            html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        }


        // Assert: the result may name the lesson, never show its body.
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Fire exits", html);
        Assert.DoesNotContain(LearningSiteFixture.SecretLessonText, html);
    }

    [Fact]
    public async Task TheQueriesApi_ReturnsNothingWithoutTheQueryPermission()
    {
        // Arrange: a query that returns lessons, as a site might add for its own pages.
        await _fixture.InTenantAsync(services => LearningSiteFixture.RunRecipeAsync(services, new JsonObject
        {
            ["name"] = "LearningLessonQuery",
            ["steps"] = new JsonArray(new JsonObject
            {
                ["name"] = "Queries",
                ["Queries"] = new JsonArray(new JsonObject
                {
                    ["Name"] = "AllPageLessons",
                    ["Source"] = "Sql",
                    ["Schema"] = "[]",
                    ["ReturnDocuments"] = true,
                    ["Template"] = "SELECT DocumentId FROM ContentItemIndex WHERE ContentType = 'PageLesson' AND Published = 1",
                }),
            }),
        }));

        using var anonymous = await _fixture.CreateClientAsync();
        using var outsider = await _fixture.CreateClientAsync("learner2");

        // Act
        var anonymousResponse = await anonymous.GetAsync("api/queries/AllPageLessons", TestContext.Current.CancellationToken);
        var outsiderResponse = await outsider.GetAsync("api/queries/AllPageLessons", TestContext.Current.CancellationToken);

        // Assert: the API answers only someone granted this query's permission, which no Learning role holds.
        Assert.NotEqual(HttpStatusCode.OK, anonymousResponse.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, outsiderResponse.StatusCode);
        Assert.DoesNotContain(LearningSiteFixture.SecretLessonText, await anonymousResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain(LearningSiteFixture.SecretLessonText, await outsiderResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
