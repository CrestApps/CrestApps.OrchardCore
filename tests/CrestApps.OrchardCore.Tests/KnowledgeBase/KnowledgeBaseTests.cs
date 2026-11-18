using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentAccessControl.Indexes;
using CrestApps.OrchardCore.ContentAccessControl.Migrations;
using CrestApps.OrchardCore.KnowledgeBase;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Migrations;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using CrestApps.OrchardCore.KnowledgeBase.Services;
using CrestApps.OrchardCore.Roles.Core.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.ContentAccessControl;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.KnowledgeBase;

/// <summary>
/// The help center on a real store, with who may read each article decided by Content Access Control: an article with
/// no role picked is for everyone, one for Authenticated for anyone signed in, and one for staff roles for those roles.
/// Search, categories and suggestions keep to what the reader may open in SQL; each searched word must be in the title
/// or summary; unpublished articles are never found. Articles written before keep who could read them.
/// </summary>
public sealed class KnowledgeBaseTests
{
    private static readonly string[] _everyone = [];
    private static readonly string[] _signedIn = ["Authenticated"];
    private static readonly string[] _staff = ["Editor", "Administrator"];

    [Fact]
    public async Task EachReader_FindsTheArticlesTheirRolesMayOpen_ByWordsInTitleOrSummary()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            await session.SaveAsync(Article("a1", "Reset your password", "Use the link on the sign-in page.", "Accounts", _everyone));
            await session.SaveAsync(Article("a2", "Download your invoices", "Invoices are under Billing.", "Billing", _signedIn));
            await session.SaveAsync(Article("a3", "Refund a customer's password reset fee", "Staff procedure.", "Accounts", _staff));
            await session.SaveAsync(Article("a4", "Old password policy", "Draft.", "Accounts", _everyone, published: false));
        });

        // Act
        var found = await harness.RunAsync(async services =>
        {
            var search = services.GetRequiredService<KnowledgeArticleSearch>();
            var ct = TestContext.Current.CancellationToken;

            return new
            {
                Visitor = (await search.FindAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, null, 10, ct)).Select(a => a.ContentItemId).Order().ToList(),
                SignedIn = (await search.FindAsync(CalendarTestHarness.User("dana"), null, null, 10, ct)).Select(a => a.ContentItemId).Order().ToList(),
                Staff = (await search.FindAsync(Staff(), "password", null, 10, ct)).Select(a => a.ContentItemId).Order().ToList(),
                Words = (await search.FindAsync(Staff(), "refund password", null, 10, ct)).Select(a => a.ContentItemId).Order().ToList(),
                Administrator = (await search.FindAsync(CalendarTestHarness.User("root", "role:Administrator"), null, null, 10, ct)).Select(a => a.ContentItemId).Order().ToList(),
                Categories = Grouped(await search.ByCategoryAsync(CalendarTestHarness.User("dana"), 5, ct)),
                StaffCategories = Grouped(await search.ByCategoryAsync(Staff(), 5, ct)),
                InAccounts = (await search.FindAsync(Staff(), null, ["Accounts"], 10, ct)).Select(a => a.ContentItemId).Order().ToList(),
                Suggested = (await search.SuggestAsync(CalendarTestHarness.User("dana"), "Have you tried to reset your password?", 5, ct)).Select(a => a.ContentItemId).ToList(),
            };
        }, commit: false);

        // Assert
        Assert.Equal(["a1"], found.Visitor);
        Assert.Equal(["a1", "a2"], found.SignedIn);
        Assert.Equal(["a1", "a3"], found.Staff);
        Assert.Equal(["a3"], found.Words);
        Assert.Equal(["a1", "a2", "a3"], found.Administrator);
        Assert.Equal(["Accounts: a1", "Billing: a2"], found.Categories);
        Assert.Equal(["Accounts: a1, a3", "Billing: a2"], found.StaffCategories);
        Assert.Equal(["a1", "a3"], found.InAccounts);
        Assert.Equal(["a1"], found.Suggested);
    }

    [Fact]
    public async Task AnAgentAnsweringACustomer_IsOfferedOnlyArticlesTheCustomerCanOpen()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            await session.SaveAsync(Article("a1", "Reset your password", "Use the link on the sign-in page.", "Accounts", _everyone));
            await session.SaveAsync(Article("a2", "Password rules for members", "Members' passwords.", "Accounts", _signedIn));
            await session.SaveAsync(Article("a3", "Password reset override", "Staff procedure.", "Accounts", _staff));
        });

        // Act
        var found = await harness.RunAsync(async services =>
        {
            var search = services.GetRequiredService<KnowledgeArticleSearch>();
            var ct = TestContext.Current.CancellationToken;

            return new
            {
                Note = (await search.SuggestAsync(Staff(), "password", 5, ct)).Select(a => a.ContentItemId).Order().ToList(),
                ToGuest = (await search.SuggestAsync(Staff(), "password", 5, KnowledgeArticleSearch.VisitorReader, ct)).Select(a => a.ContentItemId).Order().ToList(),
                ToMember = (await search.SuggestAsync(Staff(), "password", 5, KnowledgeArticleSearch.SignedInReader, ct)).Select(a => a.ContentItemId).Order().ToList(),
                VisitorToMember = (await search.SuggestAsync(new ClaimsPrincipal(new ClaimsIdentity()), "password", 5, KnowledgeArticleSearch.SignedInReader, ct)).Select(a => a.ContentItemId).Order().ToList(),
            };
        }, commit: false);

        // Assert
        Assert.Equal(["a1", "a2", "a3"], found.Note);
        Assert.Equal(["a1"], found.ToGuest);
        Assert.Equal(["a1", "a2"], found.ToMember);
        Assert.Equal(["a1"], found.VisitorToMember);
    }

    [Theory]
    [InlineData(null, new string[0])]
    [InlineData(0, new string[0])]
    [InlineData(1, new[] { "Authenticated" })]
    [InlineData(2, new[] { "Editor", "Support", "Administrator" })]
    [InlineData("Public", new string[0])]
    [InlineData("SignedIn", new[] { "Authenticated" })]
    [InlineData("Staff", new[] { "Editor", "Support", "Administrator" })]
    public void AnArticleWrittenBefore_KeepsWhoCouldReadIt(object audience, string[] roles)
    {
        // Arrange
        var item = new ContentItem { ContentItemId = "old", ContentType = KnowledgeBaseConstants.ArticleType };
        var part = new JsonObject { ["Summary"] = "Use the link on the sign-in page." };

        if (audience is not null)
        {
            part["Audience"] = audience is int number ? JsonValue.Create(number) : JsonValue.Create((string)audience);
        }

        ((JsonObject)item.Content)[KnowledgeBaseConstants.PartName] = part;

        // Act
        KnowledgeBaseMigrations.MoveAudienceToRoles(item, ["Editor", "Support"]);

        // Assert
        Assert.Equal(roles, item.GetOrCreate<RolePickerPart>().RoleNames);
        Assert.False(((JsonObject)item.Content)[KnowledgeBaseConstants.PartName].AsObject().ContainsKey("Audience"));
        Assert.Equal("Use the link on the sign-in page.", item.GetOrCreate<KnowledgeArticlePart>().Summary);
    }

    // Each category with its article ids, in order, for comparing.
    private static List<string> Grouped(IReadOnlyDictionary<string, (int Count, IReadOnlyList<KnowledgeArticleIndex> Articles)> groups)
        => [.. groups.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + ": " + string.Join(", ", pair.Value.Articles.Select(a => a.ContentItemId).Order()))];

    private static ClaimsPrincipal Staff()
        => CalendarTestHarness.User("staff", "role:Editor");

    private static Task<CalendarTestHarness> CreateAsync()
        => CalendarTestHarness.CreateAsync(
            services => services.AddScoped<KnowledgeArticleSearch>(),
            async schema =>
            {
                await schema.CreateMapIndexTableAsync<KnowledgeArticleIndex>(table => table
                    .Column<string>("ContentItemId", column => column.WithLength(26))
                    .Column<string>("Title", column => column.WithLength(255))
                    .Column<string>("Summary", column => column.WithLength(1000))
                    .Column<string>("CategoryId", column => column.Nullable().WithLength(26))
                    .Column<DateTime>("PublishedUtc", column => column.Nullable())
                    .Column<int>("HelpfulCount", column => column.Nullable()));
                await ContentAccessControlMigrations.CreateRoleIndexTableAsync(schema);
            },
            [new KnowledgeArticleIndexProvider(), new ContentAccessRoleIndexProvider(ContentAccessRoleIndexTests.DefinitionServices(KnowledgeBaseConstants.ArticleType))]);

    private static ContentItem Article(string id, string title, string summary, string category, string[] roles, bool published = true)
    {
        var item = new ContentItem
        {
            ContentItemId = id,
            ContentItemVersionId = id + "-v1",
            ContentType = KnowledgeBaseConstants.ArticleType,
            DisplayText = title,
            Published = published,
            Latest = true,
            PublishedUtc = published ? CalendarTestHarness.Now : null,
        };
        item.Apply(new KnowledgeArticlePart { Summary = summary });
        item.Weld(new RolePickerPart { RoleNames = roles });

        // The category is a term of the categories' taxonomy, in the type's Category field; here its id is the name.
        if (category is not null)
        {
            ((JsonObject)item.Content)[KnowledgeBaseConstants.ArticleType] = new JsonObject
            {
                [KnowledgeBaseConstants.CategoryField] = new JsonObject { ["TaxonomyContentItemId"] = "taxonomy", ["TermContentItemIds"] = new JsonArray(category) },
            };
        }

        return item;
    }
}
