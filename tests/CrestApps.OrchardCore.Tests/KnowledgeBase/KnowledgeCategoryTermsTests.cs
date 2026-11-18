using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentFields.Taxonomies;
using CrestApps.OrchardCore.KnowledgeBase;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Migrations;
using CrestApps.OrchardCore.KnowledgeBase.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.KnowledgeBase;

/// <summary>
/// An article's category was text; it is now a term of the Knowledge Base Categories taxonomy, so the help center lists
/// the articles of a category, and of the categories under it, together.
/// </summary>
public sealed class KnowledgeCategoryTermsTests
{
    [Fact]
    public async Task EachDistinctTextCategory_BecomesATerm_SetOnEveryVersionOfItsArticles_Once()
    {
        // Arrange: "Accounts" is already a term; "billing" and "Billing" are the same category.
        await using var harness = await CreateAsync();
        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            await session.SaveAsync(Taxonomy(("t-accounts", "Accounts")));
            await session.SaveAsync(Article("a1", "Accounts", published: true, latest: true));
            await session.SaveAsync(Article("a2", "billing", published: true, latest: false));
            await session.SaveAsync(Article("a2", "Billing", published: false, latest: true, version: "a2-draft"));
            await session.SaveAsync(Article("a3", "  ", published: true, latest: true));
            await session.SaveAsync(Article("a4", null, published: true, latest: true));

            return true;
        });

        // Act
        var converted = await ConvertAsync(harness);
        var again = await ConvertAsync(harness);

        // Assert
        // Every version that had the text property: a blank or empty one is removed without a term.
        Assert.Equal(5, converted);
        Assert.Equal(0, again);

        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            var tree = TaxonomyTermTree.From(await session.Query<ContentItem, ContentItemIndex>(index => index.ContentItemId == "taxonomy").FirstOrDefaultAsync());
            var billing = Assert.Single(tree.Nodes, node => node.Name == "billing");
            Assert.Equal(["Accounts", "billing"], tree.Nodes.Select(node => node.Name));
            Assert.Equal(KnowledgeBaseConstants.CategoryTermType, billing.Content["ContentType"]?.GetValue<string>());

            var articles = await session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == KnowledgeBaseConstants.ArticleType).ListAsync();
            Assert.Equal("t-accounts", KnowledgeArticleIndexProvider.CategoryOf(articles.Single(item => item.ContentItemId == "a1")));
            Assert.All(articles.Where(item => item.ContentItemId == "a2"), item => Assert.Equal(billing.Id, KnowledgeArticleIndexProvider.CategoryOf(item)));
            Assert.Null(KnowledgeArticleIndexProvider.CategoryOf(articles.Single(item => item.ContentItemId == "a3")));
            Assert.All(articles, item => Assert.False(((JsonObject)item.Content)[KnowledgeBaseConstants.PartName]!.AsObject().ContainsKey("Category")));

            // The published article is indexed under its term.
            Assert.Equal(billing.Id, (await session.QueryIndex<KnowledgeArticleIndex>(index => index.ContentItemId == "a2").FirstOrDefaultAsync()).CategoryId);

            return true;
        }, commit: false);
    }

    [Fact]
    public void ACategory_TakesTheCategoriesUnderIt_Along()
    {
        // Arrange: Hardware > Laptop > Battery, and Software.
        var taxonomy = Taxonomy(("hw", "Hardware"), ("sw", "Software"));
        var hardware = ((JsonObject)taxonomy.Content)["TaxonomyPart"]!["Terms"]![0]!.AsObject();
        hardware["Terms"] = new JsonArray(Term("laptop", "Laptop", new JsonArray(Term("battery", "Battery"))));
        var tree = TaxonomyTermTree.From(taxonomy);

        // Act & Assert
        Assert.Equal(["hw", "laptop", "battery"], KnowledgeBaseCategories.WithDescendants(tree, "hw"));
        Assert.Equal(["laptop", "battery"], KnowledgeBaseCategories.WithDescendants(tree, "laptop"));
        Assert.Equal(["sw"], KnowledgeBaseCategories.WithDescendants(tree, "sw"));
        Assert.Empty(KnowledgeBaseCategories.WithDescendants(tree, "gone"));
    }

    private static Task<int> ConvertAsync(CalendarTestHarness harness)
        => harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            var taxonomy = await session.Query<ContentItem, ContentItemIndex>(index => index.ContentItemId == "taxonomy").FirstOrDefaultAsync();

            return await KnowledgeCategoryTerms.ConvertAsync(session, taxonomy);
        });

    private static ContentItem Article(string id, string category, bool published, bool latest, string version = null)
    {
        var item = new ContentItem
        {
            ContentItemId = id,
            ContentItemVersionId = version ?? id + "-v1",
            ContentType = KnowledgeBaseConstants.ArticleType,
            DisplayText = id,
            Published = published,
            Latest = latest,
            PublishedUtc = published ? CalendarTestHarness.Now : null,
        };

        // What an article saved before kept: its category as text on the part.
        ((JsonObject)item.Content)[KnowledgeBaseConstants.PartName] = new JsonObject { ["Audience"] = 0, ["Category"] = category };

        return item;
    }

    private static ContentItem Taxonomy(params (string Id, string Name)[] terms)
    {
        var taxonomy = new ContentItem { ContentItemId = "taxonomy", ContentItemVersionId = "taxonomy-v1", ContentType = "Taxonomy", DisplayText = "Knowledge Base Categories", Published = true, Latest = true };
        ((JsonObject)taxonomy.Content)["TaxonomyPart"] = new JsonObject
        {
            ["TermContentType"] = KnowledgeBaseConstants.CategoryTermType,
            ["Terms"] = new JsonArray([.. terms.Select(term => (JsonNode)Term(term.Id, term.Name))]),
        };

        return taxonomy;
    }

    private static JsonObject Term(string id, string name, JsonArray children = null)
    {
        var term = new JsonObject { ["ContentItemId"] = id, ["ContentType"] = KnowledgeBaseConstants.CategoryTermType, ["DisplayText"] = name };

        if (children is not null)
        {
            term["Terms"] = children;
        }

        return term;
    }

    private static Task<CalendarTestHarness> CreateAsync()
        => CalendarTestHarness.CreateAsync(
            null,
            async schema =>
            {
                await schema.CreateMapIndexTableAsync<ContentItemIndex>(table => table
                    .Column<string>("ContentItemId", column => column.WithLength(26))
                    .Column<string>("ContentItemVersionId", column => column.WithLength(26))
                    .Column<bool>("Latest")
                    .Column<bool>("Published")
                    .Column<string>("ContentType", column => column.WithLength(255))
                    .Column<DateTime>("ModifiedUtc", column => column.Nullable())
                    .Column<DateTime>("PublishedUtc", column => column.Nullable())
                    .Column<DateTime>("CreatedUtc", column => column.Nullable())
                    .Column<string>("Owner", column => column.Nullable().WithLength(255))
                    .Column<string>("Author", column => column.Nullable().WithLength(255))
                    .Column<string>("DisplayText", column => column.Nullable().WithLength(255)));
                await schema.CreateMapIndexTableAsync<KnowledgeArticleIndex>(table => table
                    .Column<string>("ContentItemId", column => column.WithLength(26))
                    .Column<string>("Title", column => column.WithLength(255))
                    .Column<string>("Summary", column => column.Nullable().WithLength(1000))
                    .Column<string>("CategoryId", column => column.Nullable().WithLength(26))
                    .Column<int>("Audience")
                    .Column<DateTime>("PublishedUtc", column => column.Nullable())
                    .Column<int>("HelpfulCount", column => column.Nullable()));
            },
            [new ContentItemIndexProvider(), new KnowledgeArticleIndexProvider()]);
}
