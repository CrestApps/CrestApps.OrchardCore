using CrestApps.OrchardCore.AI;

namespace CrestApps.OrchardCore.Tests.Agent.BrowserAutomation;

public sealed class LivePageContextPromptBuilderTests
{
    [Fact]
    public void Append_WhenContextExists_ShouldAppendVisiblePageSummary()
    {
        using var scope = AIInvocationScope.Begin();
        LivePageContextPromptBuilder.Store(scope.Context, """
            {
              "url": "https://example.com/admin/indexes",
              "title": "Indexes",
              "isParentContext": true,
              "headings": ["Indexes"],
              "links": [
                {
                  "text": "Edit",
                  "href": "https://example.com/admin/indexes/chat-docs/edit",
                  "context": "chat-docs Edit Delete"
                }
              ],
              "buttons": [
                {
                  "text": "Add Index"
                }
              ],
              "textPreview": "chat-docs listed on the page"
            }
            """);

        var actual = LivePageContextPromptBuilder.Append("go to the edit page for chat-docs index", scope.Context);

        Assert.Contains("[Current visible page context]", actual, StringComparison.Ordinal);
        Assert.Contains("https://example.com/admin/indexes/chat-docs/edit", actual, StringComparison.Ordinal);
        Assert.Contains("chat-docs Edit Delete", actual, StringComparison.Ordinal);
    }

    [Fact]
    public void Store_WhenJsonIsInvalid_ShouldIgnoreIt()
    {
        using var scope = AIInvocationScope.Begin();
        LivePageContextPromptBuilder.Store(scope.Context, "{ invalid json");

        var actual = LivePageContextPromptBuilder.Append("go somewhere", scope.Context);

        Assert.Equal("go somewhere", actual);
    }
}
