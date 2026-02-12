using CrestApps.OrchardCore.AI.Core;

namespace CrestApps.OrchardCore.Tests.Core.Orchestration;

public sealed class LuceneTextTokenizerTests
{
    private readonly LuceneTextTokenizer _tokenizer = new();

    [Fact]
    public void Tokenize_NullOrWhitespace_ReturnsEmpty()
    {
        Assert.Empty(_tokenizer.Tokenize(null));
        Assert.Empty(_tokenizer.Tokenize(""));
        Assert.Empty(_tokenizer.Tokenize("   "));
    }

    [Fact]
    public void Tokenize_SplitsCamelCase()
    {
        var tokens = _tokenizer.Tokenize("createJiraTicket");

        Assert.Contains("creat", tokens);  // stem of "create"
        Assert.Contains("jira", tokens);
        Assert.Contains("ticket", tokens);
    }

    [Fact]
    public void Tokenize_SplitsConsecutiveUppercase()
    {
        var tokens = _tokenizer.Tokenize("JSONSchema");

        Assert.Contains("json", tokens);
        Assert.Contains("schema", tokens);
    }

    [Fact]
    public void Tokenize_AppliesStemming()
    {
        var tokensA = _tokenizer.Tokenize("recipes");
        var tokensB = _tokenizer.Tokenize("recipe");

        Assert.True(tokensA.Overlaps(tokensB),
            $"Expected overlap between [{string.Join(", ", tokensA)}] and [{string.Join(", ", tokensB)}]");
    }

    [Fact]
    public void Tokenize_RemovesStopWords()
    {
        var tokens = _tokenizer.Tokenize("the schema for this recipe");

        Assert.DoesNotContain("the", tokens);
        Assert.DoesNotContain("for", tokens);
        Assert.DoesNotContain("this", tokens);
        Assert.Contains("schema", tokens);
        Assert.Contains("recip", tokens);
    }

    [Fact]
    public void Tokenize_HandlesToolNames()
    {
        var tokens = _tokenizer.Tokenize("sendSlackMessage: Send a message to a Slack channel");

        Assert.Contains("send", tokens);
        Assert.Contains("slack", tokens);
        Assert.Contains("messag", tokens); // stem of "message"
        Assert.Contains("channel", tokens);
    }

    [Fact]
    public void Tokenize_HandlesHyphenatedText()
    {
        var tokens = _tokenizer.Tokenize("recipe-schema: Returns JSON definition");

        Assert.Contains("recip", tokens);
        Assert.Contains("schema", tokens);
        Assert.Contains("json", tokens);
        Assert.Contains("definit", tokens);
    }

    [Fact]
    public async Task Tokenize_IsThreadSafe()
    {
        // Verify the tokenizer handles concurrent calls without exceptions.
        var tasks = new Task[10];

        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < 50; j++)
                {
                    var tokens = _tokenizer.Tokenize("createJiraTicket sendSlackMessage parseJsonData");
                    Assert.True(tokens.Count > 0);
                }
            }, TestContext.Current.CancellationToken);
        }

        await Task.WhenAll(tasks);
    }
}
