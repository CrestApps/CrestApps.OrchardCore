using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.KnowledgeBase.AI;
using Moq;
using OrchardCore.ContentManagement;
using static CrestApps.OrchardCore.Tests.KnowledgeBase.AI.KnowledgeBaseAIToolHarness;

namespace CrestApps.OrchardCore.Tests.KnowledgeBase.AI;

/// <summary>
/// <c>searchKnowledgeBase</c> searches as the help center does, for the signed-in caller only: the articles their roles may
/// open (Content Access Control, in SQL), with <c>ViewContent</c> on each, every word first and then some of the longer
/// words of a question, within a category and the categories under it, and, with <c>reader</c>, only what that reader may
/// open too. It returns the allow-listed fields, the article text marked as data, local times and absolute links.
/// </summary>
public sealed class SearchKnowledgeBaseToolTests
{
    [Fact]
    public async Task Search_RefusesACallerWhoIsNotSignedIn()
    {
        // Arrange
        await using var harness = await SeedAsync();

        foreach (var caller in new[] { null, new ClaimsPrincipal(new ClaimsIdentity()) })
        {
            harness.Caller = caller;

            // Act
            var result = await harness.InvokeAsync(new SearchKnowledgeBaseTool(), new() { ["query"] = "password" });

            // Assert
            Assert.Equal(KnowledgeBaseAIToolBase.NotSignedInMessage, result);
        }

        harness.ContentManager.Verify(manager => manager.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<VersionOptions>()), Times.Never);
    }

    [Fact]
    public async Task Search_FindsOnlyTheArticlesTheCallersRolesMayOpen()
    {
        // Arrange
        await using var harness = await SeedAsync();

        // Act
        harness.Caller = Dana();
        var signedIn = Ids(await harness.InvokeAsync(new SearchKnowledgeBaseTool(), new() { ["query"] = "password" }));
        harness.Caller = StaffCaller();
        var staff = Ids(await harness.InvokeAsync(new SearchKnowledgeBaseTool(), new() { ["query"] = "password" }));

        // Assert: the draft is never found.
        Assert.Equal(["a1", "a2"], signedIn.Order());
        Assert.Equal(["a1", "a2", "a3"], staff.Order());
    }

    [Fact]
    public async Task Search_LeavesOutAnArticleTheCallerMayNotView()
    {
        // Arrange
        await using var harness = await SeedAsync();
        harness.Caller = Dana();
        harness.Refused.Add("a2");

        // Act
        var found = Ids(await harness.InvokeAsync(new SearchKnowledgeBaseTool(), new() { ["query"] = "password" }));

        // Assert
        Assert.Equal(["a1"], found);
    }

    [Fact]
    public async Task Search_ForAReader_ReturnsOnlyWhatThatReaderMayOpenToo()
    {
        // Arrange
        await using var harness = await SeedAsync();
        harness.Caller = StaffCaller();

        // Act: the all-words search, and a question that only the suggestions match.
        var toVisitor = Ids(await harness.InvokeAsync(new SearchKnowledgeBaseTool(), new() { ["query"] = "password", ["reader"] = "public" }));
        var toMember = Ids(await harness.InvokeAsync(new SearchKnowledgeBaseTool(), new() { ["query"] = "password", ["reader"] = "signedIn" }));
        var questionToVisitor = Ids(await harness.InvokeAsync(new SearchKnowledgeBaseTool(), new() { ["query"] = "How do I reset my password?", ["reader"] = "public" }));
        var unknown = await harness.InvokeAsync(new SearchKnowledgeBaseTool(), new() { ["query"] = "password", ["reader"] = "everyone" });

        // Assert
        Assert.Equal(["a1"], toVisitor);
        Assert.Equal(["a1", "a2"], toMember.Order());
        Assert.Equal(["a1"], questionToVisitor);
        Assert.StartsWith("Nothing was done", unknown);
    }

    [Fact]
    public async Task Search_PutsTheArticlesWithEveryWordFirst_ThenThoseWithSomeOfAQuestionsWords()
    {
        // Arrange
        await using var harness = await SeedAsync();
        harness.Caller = StaffCaller();

        // Act
        var words = Items(await harness.InvokeAsync(new SearchKnowledgeBaseTool(), new() { ["query"] = "reset password" }));
        var question = Items(await harness.InvokeAsync(new SearchKnowledgeBaseTool(), new() { ["query"] = "How do I reset my password?" }));

        // Assert: a3 is more helpful than a1; a2 has only "password".
        Assert.Equal(["a3:allWords", "a1:allWords", "a2:someWords"], words.Select(item => item.GetProperty("id").GetString() + ":" + item.GetProperty("match").GetString()));
        Assert.Equal(["a3:someWords", "a1:someWords", "a2:someWords"], question.Select(item => item.GetProperty("id").GetString() + ":" + item.GetProperty("match").GetString()));
    }

    [Fact]
    public async Task Search_InACategory_TakesTheCategoriesUnderIt_ByIdNameOrPath()
    {
        // Arrange
        await using var harness = await SeedAsync();
        harness.Caller = StaffCaller();

        // Act
        var accounts = await harness.InvokeAsync(new SearchKnowledgeBaseTool(), new() { ["category"] = "accounts" });
        var passwords = Ids(await harness.InvokeAsync(new SearchKnowledgeBaseTool(), new() { ["category"] = "Accounts > Passwords", ["query"] = "password" }));
        var billing = Ids(await harness.InvokeAsync(new SearchKnowledgeBaseTool(), new() { ["category"] = BillingCategory }));

        // Assert
        var json = ReadJson(accounts);
        Assert.Equal(["a1", "a2", "a3"], Ids(accounts).Order());
        Assert.All(json.GetProperty("items").EnumerateArray(), item => Assert.Equal("browse", item.GetProperty("match").GetString()));
        Assert.Equal("Accounts", json.GetProperty("category").GetProperty("name").GetString());
        Assert.Equal(["a1", "a3"], passwords.Order());
        Assert.Equal(["a4"], billing);
    }

    [Fact]
    public async Task Search_RefusesAnUnknownCategory_AndListsTheCategories()
    {
        // Arrange
        await using var harness = await SeedAsync();
        harness.Caller = Dana();

        // Act
        var result = ReadJson(await harness.InvokeAsync(new SearchKnowledgeBaseTool(), new() { ["category"] = "Shipping", ["query"] = "password" }));

        // Assert
        Assert.StartsWith("Nothing was searched", result.GetProperty("message").GetString());
        Assert.False(result.TryGetProperty("items", out _));
        Assert.Equal(
            ["Accounts:2", "Accounts › Passwords:1", "Billing:1"],
            result.GetProperty("categories").EnumerateArray().Select(category => category.GetProperty("name").GetString() + ":" + category.GetProperty("articles").GetInt32()));
    }

    [Fact]
    public async Task Search_ReturnsOnlyTheAllowListedFields_WithTheTextMarkedAsData()
    {
        // Arrange
        await using var harness = await SeedAsync();
        harness.Caller = Dana();

        // Act
        var result = await harness.InvokeAsync(new SearchKnowledgeBaseTool(), new() { ["query"] = "password", ["take"] = 1 });

        // Assert
        var json = ReadJson(result);
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());
        Assert.Equal(
            ["category", "helpfulCount", "id", "link", "match", "notHelpfulCount", "passage", "publishedAt", "summary", "title"],
            item.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("a1", item.GetProperty("id").GetString());
        Assert.Equal("Reset your password", item.GetProperty("title").GetString());
        Assert.Equal("Accounts › Passwords", item.GetProperty("category").GetProperty("name").GetString());
        Assert.Equal("https://example.com/help/a1", item.GetProperty("link").GetString());
        Assert.Equal("2026-11-02T07:00:00-05:00", item.GetProperty("publishedAt").GetString());

        var passage = item.GetProperty("passage").GetString();
        Assert.Contains("password page", passage);
        Assert.StartsWith("…", passage);
        Assert.True(passage.Length <= KnowledgeArticleProjection.PassageLength + 2);

        Assert.True(json.GetProperty("truncated").GetBoolean());
        Assert.Equal(1, json.GetProperty("returned").GetInt32());
        Assert.Equal(KnowledgeBaseAIToolBase.DataNote, json.GetProperty("dataNote").GetString());
        Assert.DoesNotContain("RolePicker", result);
        Assert.DoesNotContain("Authenticated", result);
    }

    [Fact]
    public async Task Search_SaysSoWhenNothingMatches_AndRefusesAnUndeclaredArgument()
    {
        // Arrange
        await using var harness = await SeedAsync();
        harness.Caller = Dana();

        // Act
        var none = ReadJson(await harness.InvokeAsync(new SearchKnowledgeBaseTool(), new() { ["query"] = "shipping label" }));
        var undeclared = await harness.InvokeAsync(new SearchKnowledgeBaseTool(), new() { ["q"] = "password" });

        // Assert
        Assert.Equal(0, none.GetProperty("returned").GetInt32());
        Assert.Contains("No published article", none.GetProperty("message").GetString());
        Assert.NotEmpty(none.GetProperty("categories").EnumerateArray());
        Assert.Contains("'q' is not an argument", undeclared);
    }

    // a1 for everyone, a2 for signed-in people, a3 for staff, a4 in Billing for everyone, and a draft.
    private static async Task<KnowledgeBaseAIToolHarness> SeedAsync()
    {
        var harness = await CreateAsync();
        var body = string.Concat(Enumerable.Repeat("Some words about the account settings. ", 20)) + "Open the password page and choose Reset. " + string.Concat(Enumerable.Repeat("More words about other settings. ", 20));

        await harness.AddAsync(
            Article("a1", "Reset your password", "Use the link on the sign-in page.", PasswordsCategory, ForEveryone, body, helpful: 1),
            Article("a2", "Password rules for members", "What a member's password must hold.", AccountsCategory, ForSignedIn),
            Article("a3", "Password reset override", "Staff procedure.", PasswordsCategory, ForStaff, helpful: 3),
            Article("a4", "Download your invoices", "Invoices are under Billing.", BillingCategory, ForEveryone),
            Article("a5", "Old password policy", "Draft.", PasswordsCategory, ForEveryone, published: false));

        return harness;
    }

    private static List<JsonElement> Items(string result)
        => [.. ReadJson(result).GetProperty("items").EnumerateArray()];

    private static List<string> Ids(string result)
        => [.. Items(result).Select(item => item.GetProperty("id").GetString())];
}
