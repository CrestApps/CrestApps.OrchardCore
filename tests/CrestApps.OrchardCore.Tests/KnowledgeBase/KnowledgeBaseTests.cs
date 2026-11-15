using System.Security.Claims;
using CrestApps.OrchardCore.KnowledgeBase;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using CrestApps.OrchardCore.KnowledgeBase.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.KnowledgeBase;

/// <summary>
/// The help center on a real store: a visitor finds public articles only, a signed-in person also those for signed-in
/// people, and staff also staff-only ones; each searched word must be in the title or summary; unpublished articles are
/// never found; and viewing a staff-only article fails for anyone without the staff permission.
/// </summary>
public sealed class KnowledgeBaseTests
{
    [Fact]
    public async Task EachReader_FindsTheArticlesOfTheirAudience_ByWordsInTitleOrSummary()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            await session.SaveAsync(Article("a1", "Reset your password", "Use the link on the sign-in page.", "Accounts", KnowledgeArticleAudience.Public));
            await session.SaveAsync(Article("a2", "Download your invoices", "Invoices are under Billing.", "Billing", KnowledgeArticleAudience.SignedIn));
            await session.SaveAsync(Article("a3", "Refund a customer's password reset fee", "Staff procedure.", "Accounts", KnowledgeArticleAudience.Staff));
            await session.SaveAsync(Article("a4", "Old password policy", "Draft.", "Accounts", KnowledgeArticleAudience.Public, published: false));
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
                Staff = (await search.FindAsync(CalendarTestHarness.User("staff"), "password", null, 10, ct)).Select(a => a.ContentItemId).Order().ToList(),
                Words = (await search.FindAsync(CalendarTestHarness.User("staff"), "refund password", null, 10, ct)).Select(a => a.ContentItemId).Order().ToList(),
                Categories = await search.CategoriesAsync(CalendarTestHarness.User("dana"), ct),
            };
        }, commit: false);

        // Assert
        Assert.Equal(["a1"], found.Visitor);
        Assert.Equal(["a1", "a2"], found.SignedIn);
        Assert.Equal(["a1", "a3"], found.Staff);
        Assert.Equal(["a3"], found.Words);
        Assert.Equal([("Accounts", 1), ("Billing", 1)], found.Categories);
    }

    [Fact]
    public async Task ViewingAStaffOnlyArticle_FailsForAnyoneWithoutTheStaffPermission()
    {
        // Arrange
        var authorization = Authorization();
        var services = new ServiceCollection()
            .AddSingleton(authorization)
            .AddScoped<KnowledgeArticleAccess>()
            .BuildServiceProvider();
        var handler = new KnowledgeArticleAuthorizationHandler(services);
        var staffOnly = Article("a3", "Staff procedure", null, null, KnowledgeArticleAudience.Staff);

        // Act
        var forDana = new AuthorizationHandlerContext([new PermissionRequirement(CommonPermissions.ViewContent)], CalendarTestHarness.User("dana"), staffOnly);
        var forStaff = new AuthorizationHandlerContext([new PermissionRequirement(CommonPermissions.ViewContent)], CalendarTestHarness.User("staff"), staffOnly);
        await handler.HandleAsync(forDana);
        await handler.HandleAsync(forStaff);

        // Assert
        Assert.True(forDana.HasFailed);
        Assert.False(forStaff.HasFailed);
    }

    private static Task<CalendarTestHarness> CreateAsync()
        => CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddSingleton(Authorization());
                services.AddScoped<KnowledgeArticleAccess>();
                services.AddScoped<KnowledgeArticleSearch>();
            },
            async schema =>
            {
                await schema.CreateMapIndexTableAsync<KnowledgeArticleIndex>(table => table
                    .Column<string>("ContentItemId", column => column.WithLength(26))
                    .Column<string>("Title", column => column.WithLength(255))
                    .Column<string>("Summary", column => column.WithLength(1000))
                    .Column<string>("Category", column => column.WithLength(100))
                    .Column<int>("Audience")
                    .Column<DateTime>("PublishedUtc", column => column.Nullable())
                    .Column<int>("HelpfulCount", column => column.Nullable()));
            },
            [new KnowledgeArticleIndexProvider()]);

    // The "staff" user holds the staff permission.
    private static IAuthorizationService Authorization()
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().Any(requirement => requirement.Permission.Name == KnowledgeBasePermissions.ViewStaffArticles.Name)
                    && user.FindFirstValue(ClaimTypes.NameIdentifier) == "staff"
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorization.Object;
    }

    private static ContentItem Article(string id, string title, string summary, string category, KnowledgeArticleAudience audience, bool published = true)
    {
        var item = new ContentItem
        {
            ContentItemId = id,
            ContentType = KnowledgeBaseConstants.ArticleType,
            DisplayText = title,
            Published = published,
            Latest = true,
            PublishedUtc = published ? CalendarTestHarness.Now : null,
        };
        item.Apply(new KnowledgeArticlePart { Audience = audience, Category = category, Summary = summary });

        return item;
    }
}
