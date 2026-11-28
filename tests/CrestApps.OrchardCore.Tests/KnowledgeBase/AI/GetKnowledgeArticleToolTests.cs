using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.KnowledgeBase.AI;
using Moq;
using OrchardCore.ContentManagement;
using static CrestApps.OrchardCore.Tests.KnowledgeBase.AI.KnowledgeBaseAIToolHarness;

namespace CrestApps.OrchardCore.Tests.KnowledgeBase.AI;

/// <summary>
/// <c>getKnowledgeArticle</c> reads the published version of an article the signed-in caller may view, in full, with the
/// allow-listed fields and the text marked as data; anything else gets the same answer as an id that does not exist.
/// </summary>
public sealed class GetKnowledgeArticleToolTests
{
    [Fact]
    public async Task Get_RefusesACallerWhoIsNotSignedIn()
    {
        // Arrange
        await using var harness = await SeedAsync();
        harness.Caller = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await harness.InvokeAsync(new GetKnowledgeArticleTool(), new() { ["articleId"] = "a1" });

        // Assert
        Assert.Equal(KnowledgeBaseAIToolBase.NotSignedInMessage, result);
        harness.ContentManager.Verify(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()), Times.Never);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("a5")]
    [InlineData("page")]
    [InlineData("a3")]
    public async Task Get_AnswersTheSame_ForAnythingButAPublishedArticleTheCallerMayView(string articleId)
    {
        // Arrange: a5 is a draft, page is not an article, and the caller may not view a3.
        await using var harness = await SeedAsync();
        harness.Caller = Dana();
        harness.Refused.Add("a3");

        // Act
        var result = await harness.InvokeAsync(new GetKnowledgeArticleTool(), new() { ["articleId"] = articleId });

        // Assert
        Assert.Equal(GetKnowledgeArticleTool.NotFoundMessage, result);
    }

    [Fact]
    public async Task Get_ReturnsTheWholeArticle_WithOnlyTheAllowListedFields()
    {
        // Arrange
        await using var harness = await SeedAsync();
        harness.Caller = Dana();

        // Act
        var result = await harness.InvokeAsync(new GetKnowledgeArticleTool(), new() { ["articleId"] = "a1" });

        // Assert
        var json = ReadJson(result);
        Assert.Equal(
            ["body", "bodyTruncated", "category", "dataNote", "helpfulCount", "id", "link", "notHelpfulCount", "publishedAt", "summary", "title"],
            json.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Reset your password", json.GetProperty("title").GetString());
        Assert.Equal("Use the link on the sign-in page.", json.GetProperty("summary").GetString());
        Assert.Equal("Open the password page and choose Reset.", json.GetProperty("body").GetString());
        Assert.False(json.GetProperty("bodyTruncated").GetBoolean());
        Assert.Equal(PasswordsCategory, json.GetProperty("category").GetProperty("id").GetString());
        Assert.Equal("Accounts › Passwords", json.GetProperty("category").GetProperty("name").GetString());
        Assert.Equal("https://example.com/help/a1", json.GetProperty("link").GetString());
        Assert.Equal("2026-11-02T07:00:00-05:00", json.GetProperty("publishedAt").GetString());
        Assert.Equal(2, json.GetProperty("helpfulCount").GetInt32());
        Assert.Equal(KnowledgeBaseAIToolBase.DataNote, json.GetProperty("dataNote").GetString());
        Assert.DoesNotContain("RolePicker", result);
    }

    [Fact]
    public async Task Get_CutsALongBody_AndSaysSo()
    {
        // Arrange
        await using var harness = await SeedAsync();
        harness.Caller = Dana();
        await harness.AddAsync(Article("long", "A long article", "Everything.", null, ForEveryone, new string('x', GetKnowledgeArticleTool.MaxBodyLength + 500)));

        // Act
        var json = ReadJson(await harness.InvokeAsync(new GetKnowledgeArticleTool(), new() { ["articleId"] = "long" }));

        // Assert
        Assert.Equal(GetKnowledgeArticleTool.MaxBodyLength, json.GetProperty("body").GetString().Length);
        Assert.True(json.GetProperty("bodyTruncated").GetBoolean());
        Assert.False(json.TryGetProperty("category", out _));
    }

    // a1 for everyone, a3 for staff, a5 a draft, and a page that is not an article.
    private static async Task<KnowledgeBaseAIToolHarness> SeedAsync()
    {
        var harness = await CreateAsync();
        var page = new ContentItem { ContentItemId = "page", ContentItemVersionId = "page-v1", ContentType = "Page", DisplayText = "About", Published = true, Latest = true };
        ((JsonObject)page.Content)["TitlePart"] = new JsonObject { ["Title"] = "About" };

        await harness.AddAsync(
            Article("a1", "Reset your password", "Use the link on the sign-in page.", PasswordsCategory, ForEveryone, "Open the password page and choose Reset.", helpful: 2),
            Article("a3", "Password reset override", "Staff procedure.", PasswordsCategory, ForStaff),
            Article("a5", "Old password policy", "Draft.", PasswordsCategory, ForEveryone, published: false),
            page);

        return harness;
    }
}
