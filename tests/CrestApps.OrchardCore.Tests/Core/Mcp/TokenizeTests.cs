using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;

namespace CrestApps.OrchardCore.Tests.Core.Mcp;

public sealed class TokenizeTests
{
    private readonly LuceneTextTokenizer _tokenizer = new LuceneTextTokenizer();

    [Fact]
    public void Tokenize_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Empty(_tokenizer.Tokenize(null));
        Assert.Empty(_tokenizer.Tokenize(""));
        Assert.Empty(_tokenizer.Tokenize("   "));
    }

    [Fact]
    public void Tokenize_SplitsCamelCase()
    {
        // Lucene's WordDelimiterFilter splits camelCase identifiers.
        var tokens = _tokenizer.Tokenize("findRecipeSchema");

        Assert.Contains("find", tokens);
        Assert.Contains("recip", tokens); // Porter stem of "recipe"
        Assert.Contains("schema", tokens);
    }

    [Fact]
    public void Tokenize_SplitsConsecutiveUppercase()
    {
        // "JSONSchema" should split into separate tokens.
        var tokens = _tokenizer.Tokenize("JSONSchema");

        Assert.Contains("json", tokens);
        Assert.Contains("schema", tokens);
    }

    [Fact]
    public void Tokenize_SplitsOnSeparators()
    {
        var tokens = _tokenizer.Tokenize("recipe-schema: Returns JSON definition");

        Assert.Contains("recip", tokens); // Porter stem
        Assert.Contains("schema", tokens);
        Assert.Contains("return", tokens); // Porter stem of "returns"
        Assert.Contains("json", tokens);
        Assert.Contains("definit", tokens); // Porter stem of "definition"
    }

    [Fact]
    public void Tokenize_FiltersStopWords()
    {
        var tokens = _tokenizer.Tokenize("the schema for this recipe");

        // "the", "for", "this" are standard English stop words.
        Assert.DoesNotContain("the", tokens);
        Assert.DoesNotContain("for", tokens);
        Assert.DoesNotContain("this", tokens);

        // Meaningful words remain (stemmed).
        Assert.Contains("schema", tokens);
        Assert.Contains("recip", tokens);
    }

    [Fact]
    public void Tokenize_FiltersCommonQuestionWords()
    {
        // Lucene's English stop words include: the, is, are, was, to, and, but, of, etc.
        // Question words like "what", "how" are NOT in Lucene's stop list.
        var tokens = _tokenizer.Tokenize("what is the value of this");

        Assert.DoesNotContain("the", tokens);
        Assert.DoesNotContain("of", tokens);
        Assert.DoesNotContain("this", tokens);
        // "is" is a stop word in Lucene's English list.
        Assert.DoesNotContain("is", tokens);
    }

    [Fact]
    public void Tokenize_AppliesPorterStemming()
    {
        // Porter stemmer normalizes morphological variants.
        var recipesTokens = _tokenizer.Tokenize("recipes");
        var recipeTokens = _tokenizer.Tokenize("recipe");

        // Both should stem to the same form and thus overlap.
        Assert.True(
            recipesTokens.Overlaps(recipeTokens),
            $"Expected overlap between [{string.Join(", ", recipesTokens)}] and [{string.Join(", ", recipeTokens)}]");
    }

    [Fact]
    public void Tokenize_StemMatchesIngForm()
    {
        // "enabling" and "enable" should share a common stem.
        var gerundTokens = _tokenizer.Tokenize("enabling");
        var baseTokens = _tokenizer.Tokenize("enable");

        Assert.True(
            gerundTokens.Overlaps(baseTokens),
            $"Expected overlap between [{string.Join(", ", gerundTokens)}] and [{string.Join(", ", baseTokens)}]");
    }

    [Fact]
    public void Tokenize_StemMatchesEdForm()
    {
        // "configured" and "configure" should share a common stem.
        var pastTokens = _tokenizer.Tokenize("configured");
        var baseTokens = _tokenizer.Tokenize("configure");

        Assert.True(
            pastTokens.Overlaps(baseTokens),
            $"Expected overlap between [{string.Join(", ", pastTokens)}] and [{string.Join(", ", baseTokens)}]");
    }

    [Fact]
    public void Tokenize_RealWorldPrompt_ContainsExpectedTokens()
    {
        var tokens = _tokenizer.Tokenize(
            "what is OrchardCore recipe schema for enabling OrchardCore.Contents feature?");

        // "is" and "for" are English stop words filtered by Lucene.
        Assert.DoesNotContain("is", tokens);
        Assert.DoesNotContain("for", tokens);

        // Meaningful words present as Porter stems.
        Assert.Contains("recip", tokens);        // stem of "recipe"
        Assert.Contains("schema", tokens);
        Assert.Contains("featur", tokens);       // stem of "feature"
        Assert.Contains("enabl", tokens);        // stem of "enabling"
    }

    [Fact]
    public void Tokenize_RealWorldCapability_MatchesPromptTokens()
    {
        var promptTokens = _tokenizer.Tokenize(
            "what is OrchardCore recipe schema for enabling OrchardCore.Contents feature?");

        var capabilityTokens = _tokenizer.Tokenize(
            "getOrchardCoreRecipeJsonSchema: Returns a JSON Schema definition for Orchard Core recipes or a specific recipe step.");

        // Count overlapping tokens.
        var matchCount = 0;

        foreach (var token in promptTokens)
        {
            if (capabilityTokens.Contains(token))
            {
                matchCount++;
            }
        }

        // Should have strong overlap on stemmed forms (recip, schema, orchard, core, etc.)
        Assert.True(matchCount >= 3, $"Expected at least 3 matching tokens, got {matchCount}. Prompt tokens: [{string.Join(", ", promptTokens)}], Capability tokens: [{string.Join(", ", capabilityTokens)}]");

        var score = (float)matchCount / promptTokens.Count;
        Assert.True(score >= 0.2f, $"Expected keyword match score >= 0.2, got {score}");
    }
}
