using CrestApps.AI.Prompting;
using CrestApps.AI.Prompting.Parsing;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.AI.Prompting;

public sealed class DefaultMarkdownAITemplateParserTests
{
    private readonly DefaultMarkdownAITemplateParser _parser;

    public DefaultMarkdownAITemplateParserTests()
    {
        _parser = new DefaultMarkdownAITemplateParser();
    }

    [Fact]
    public void Parse_NullContent_ReturnsEmptyBody()
    {
        var result = _parser.Parse(null);

        Assert.Equal(string.Empty, result.Body);
        Assert.NotNull(result.Metadata);
    }

    [Fact]
    public void Parse_EmptyContent_ReturnsEmptyBody()
    {
        var result = _parser.Parse(string.Empty);

        Assert.Equal(string.Empty, result.Body);
    }

    [Fact]
    public void Parse_WhitespaceContent_ReturnsEmptyBody()
    {
        var result = _parser.Parse("   \n  \n  ");

        Assert.Equal(string.Empty, result.Body);
    }

    [Fact]
    public void Parse_NoFrontMatter_ReturnsBodyAsIs()
    {
        var content = "You are a helpful assistant.";

        var result = _parser.Parse(content);

        Assert.Equal("You are a helpful assistant.", result.Body);
        Assert.Null(result.Metadata.Title);
        Assert.Null(result.Metadata.Description);
    }

    [Fact]
    public void Parse_WithFrontMatter_ExtractsMetadataAndBody()
    {
        var content = """
            ---
            Title: My Prompt
            Description: A helpful prompt
            ---
            You are a helpful assistant.
            """;

        var result = _parser.Parse(content);

        Assert.Equal("My Prompt", result.Metadata.Title);
        Assert.Equal("A helpful prompt", result.Metadata.Description);
        Assert.Equal("You are a helpful assistant.", result.Body);
    }

    [Fact]
    public void Parse_WithAllMetadataFields_ExtractsAllFields()
    {
        var content = """
            ---
            Title: Chart Generator
            Description: Generates Chart.js configs
            IsListable: true
            Category: Data Visualization
            ---
            You are a data visualization expert.
            """;

        var result = _parser.Parse(content);

        Assert.Equal("Chart Generator", result.Metadata.Title);
        Assert.Equal("Generates Chart.js configs", result.Metadata.Description);
        Assert.True(result.Metadata.IsListable);
        Assert.Equal("Data Visualization", result.Metadata.Category);
        Assert.Equal("You are a data visualization expert.", result.Body);
    }

    [Fact]
    public void Parse_IsListable_DefaultsToTrue()
    {
        var content = """
            ---
            Title: A Prompt
            ---
            Body here.
            """;

        var result = _parser.Parse(content);

        Assert.True(result.Metadata.IsListable);
    }

    [Fact]
    public void Parse_IsListableFalse_SetsFalse()
    {
        var content = """
            ---
            IsListable: false
            ---
            Body here.
            """;

        var result = _parser.Parse(content);

        Assert.False(result.Metadata.IsListable);
    }

    [Fact]
    public void Parse_CustomProperties_GoToAdditionalProperties()
    {
        var content = """
            ---
            Title: Test
            CustomKey: CustomValue
            AnotherKey: AnotherValue
            ---
            Body content.
            """;

        var result = _parser.Parse(content);

        Assert.Equal("Test", result.Metadata.Title);
        Assert.Equal("CustomValue", result.Metadata.AdditionalProperties["CustomKey"]);
        Assert.Equal("AnotherValue", result.Metadata.AdditionalProperties["AnotherKey"]);
    }

    [Fact]
    public void Parse_MissingClosingDelimiter_TreatsAsBody()
    {
        var content = """
            ---
            Title: My Prompt
            You are a helpful assistant.
            """;

        var result = _parser.Parse(content);

        // Without closing ---, the whole thing should be treated as body
        Assert.Contains("You are a helpful assistant.", result.Body);
    }

    [Fact]
    public void Parse_EmptyFrontMatter_ReturnsBody()
    {
        var content = """
            ---
            ---
            Body here.
            """;

        var result = _parser.Parse(content);

        Assert.Equal("Body here.", result.Body);
        Assert.Null(result.Metadata.Title);
    }

    [Fact]
    public void Parse_MultiLineBody_PreservesContent()
    {
        var content = """
            ---
            Title: Multi-line
            ---
            Line one.
            Line two.
            Line three.
            """;

        var result = _parser.Parse(content);

        Assert.Equal("Multi-line", result.Metadata.Title);
        Assert.Contains("Line one.", result.Body);
        Assert.Contains("Line two.", result.Body);
        Assert.Contains("Line three.", result.Body);
    }

    [Fact]
    public void Parse_MetadataKeysCaseInsensitive_SetsCorrectly()
    {
        var content = """
            ---
            title: Lower Case Title
            DESCRIPTION: Upper case desc
            islistable: false
            category: Test
            ---
            Body.
            """;

        var result = _parser.Parse(content);

        Assert.Equal("Lower Case Title", result.Metadata.Title);
        Assert.Equal("Upper case desc", result.Metadata.Description);
        Assert.False(result.Metadata.IsListable);
        Assert.Equal("Test", result.Metadata.Category);
    }

    [Fact]
    public void Parse_ValueWithColons_PreservesFullValue()
    {
        var content = """
            ---
            Title: A Title: With Colons
            ---
            Body.
            """;

        var result = _parser.Parse(content);

        Assert.Equal("A Title: With Colons", result.Metadata.Title);
    }

    [Fact]
    public void Parse_InvalidIsListable_KeepsDefault()
    {
        var content = """
            ---
            IsListable: notabool
            ---
            Body.
            """;

        var result = _parser.Parse(content);

        // Default is true; invalid value should not change it.
        Assert.True(result.Metadata.IsListable);
    }

    [Fact]
    public void Parse_EmptyMetadataValues_SetEmptyStrings()
    {
        var content = """
            ---
            Title:
            Description:
            ---
            Body.
            """;

        var result = _parser.Parse(content);

        Assert.Equal(string.Empty, result.Metadata.Title);
        Assert.Equal(string.Empty, result.Metadata.Description);
    }

    [Fact]
    public void Parse_LineWithNoColon_IsSkipped()
    {
        var content = """
            ---
            Title: Valid
            This line has no colon
            Description: Also Valid
            ---
            Body.
            """;

        var result = _parser.Parse(content);

        Assert.Equal("Valid", result.Metadata.Title);
        Assert.Equal("Also Valid", result.Metadata.Description);
    }

    [Fact]
    public void Parse_WhitespaceAroundValues_IsTrimmed()
    {
        var content = """
            ---
            Title:   Lots of spaces   
            Description:    Also spaces    
            ---
            Body.
            """;

        var result = _parser.Parse(content);

        Assert.Equal("Lots of spaces", result.Metadata.Title);
        Assert.Equal("Also spaces", result.Metadata.Description);
    }

    [Fact]
    public void Parse_CompactsJsonFencedBlocks()
    {
        var content = """
            ---
            Title: Test
            ---

            [Output Format]
            ```json
            {
                "type": "bar",
                "data": {
                    "labels": ["Jan", "Feb"]
                }
            }
            ```
            """;

        var result = _parser.Parse(content);

        Assert.Contains("""```json""", result.Body);
        Assert.Contains("""{"type":"bar","data":{"labels":["Jan","Feb"]}}""", result.Body);
        Assert.DoesNotContain("    ", result.Body);
    }

    [Fact]
    public void Parse_CompactsMultipleJsonFencedBlocks()
    {
        var content = """
            ---
            Title: Test
            ---

            First block:
            ```json
            {
                "a": 1
            }
            ```

            Second block:
            ```json
            {
                "b": 2
            }
            ```
            """;

        var result = _parser.Parse(content);

        Assert.Contains("""{"a":1}""", result.Body);
        Assert.Contains("""{"b":2}""", result.Body);
    }

    [Fact]
    public void Parse_PreservesNonJsonFencedBlocks()
    {
        var content = """
            ---
            Title: Test
            ---

            ```csharp
            var x = new {
                Name = "test"
            };
            ```
            """;

        var result = _parser.Parse(content);

        Assert.Contains("var x = new {", result.Body);
    }

    [Fact]
    public void Parse_InvalidJsonInFence_PreservesAsIs()
    {
        var content = """
            ---
            Title: Test
            ---

            ```json
            { not valid json }
            ```
            """;

        var result = _parser.Parse(content);

        Assert.Contains("{ not valid json }", result.Body);
    }

    [Fact]
    public void Parse_AlreadyCompactJson_LeavesUnchanged()
    {
        var content = """
            ---
            Title: Test
            ---

            ```json
            {"type":"bar","data":{"labels":["Jan"]}}
            ```
            """;

        var result = _parser.Parse(content);

        Assert.Contains("""{"type":"bar","data":{"labels":["Jan"]}}""", result.Body);
    }

    [Fact]
    public void Parse_JsonArrayInFence_CompactsCorrectly()
    {
        var content = """
            ---
            Title: Test
            ---

            ```json
            [
                "one",
                "two",
                "three"
            ]
            ```
            """;

        var result = _parser.Parse(content);

        Assert.Contains("""["one","two","three"]""", result.Body);
    }

    [Fact]
    public void Parse_NoFrontMatter_StillCompactsJson()
    {
        var content = """
            Some text before.

            ```json
            {
                "key": "value"
            }
            ```
            """;

        var result = _parser.Parse(content);

        Assert.Contains("""{"key":"value"}""", result.Body);
    }

    [Fact]
    public void CompactJsonBlocks_EmptyString_ReturnsEmpty()
    {
        var result = DefaultMarkdownAITemplateParser.CompactJsonBlocks(string.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void CompactJsonBlocks_NullString_ReturnsNull()
    {
        var result = DefaultMarkdownAITemplateParser.CompactJsonBlocks(null);

        Assert.Null(result);
    }

    [Fact]
    public void CompactJsonBlocks_UnclosedFence_LeavesAsIs()
    {
        var content = """
            ```json
            { "key": "value" }
            """;

        var result = DefaultMarkdownAITemplateParser.CompactJsonBlocks(content);

        Assert.Contains("""{ "key": "value" }""", result);
    }

    [Fact]
    public void CompactJsonBlocks_DeeplyNestedJson_CompactsCorrectly()
    {
        var content = """
            ```json
            {
                "level1": {
                    "level2": {
                        "level3": {
                            "value": "deep"
                        }
                    }
                }
            }
            ```
            """;

        var result = DefaultMarkdownAITemplateParser.CompactJsonBlocks(content);

        Assert.Contains("""{"level1":{"level2":{"level3":{"value":"deep"}}}}""", result);
    }

    [Fact]
    public void CompactJsonBlocks_JsonWithMixedTypes_CompactsCorrectly()
    {
        var content = """
            ```json
            {
                "name": "test",
                "count": 42,
                "active": true,
                "tags": null,
                "scores": [1, 2, 3]
            }
            ```
            """;

        var result = DefaultMarkdownAITemplateParser.CompactJsonBlocks(content);

        Assert.Contains("""{"name":"test","count":42,"active":true,"tags":null,"scores":[1,2,3]}""", result);
    }

    [Fact]
    public void CompactJsonBlocks_EmptyJsonObject_LeavesAsIs()
    {
        var content = """
            ```json
            {}
            ```
            """;

        var result = DefaultMarkdownAITemplateParser.CompactJsonBlocks(content);

        Assert.Contains("{}", result);
    }

    [Fact]
    public void CompactJsonBlocks_SchemaDescriptionWithPipe_PreservesAsIs()
    {
        var content = """
            ```json
            {
              "Concluded": true | false,
              "DispositionId": "<id_or_null>"
            }
            ```
            """;

        var result = DefaultMarkdownAITemplateParser.CompactJsonBlocks(content);

        // Contains pipe operator making it invalid JSON; should be preserved as-is
        Assert.Contains("true | false", result);
    }

    [Fact]
    public void CompactJsonBlocks_MixedValidAndInvalidBlocks_CompactsOnlyValid()
    {
        var content = """
            Valid block:
            ```json
            {
                "key": "value"
            }
            ```

            Invalid block:
            ```json
            { not valid }
            ```
            """;

        var result = DefaultMarkdownAITemplateParser.CompactJsonBlocks(content);

        Assert.Contains("""{"key":"value"}""", result);
        Assert.Contains("{ not valid }", result);
    }

    [Fact]
    public void CompactJsonBlocks_JsonWithUnicodeAndEscapes_CompactsCorrectly()
    {
        var content = """
            ```json
            {
                "greeting": "Hello, \"world\"!",
                "emoji": "\u2764"
            }
            ```
            """;

        var result = DefaultMarkdownAITemplateParser.CompactJsonBlocks(content);

        Assert.Contains("\"greeting\":", result);
        Assert.Contains("\"emoji\":", result);
        Assert.DoesNotContain("    ", result);
    }

    [Fact]
    public void Parse_JsonBlockInBodyWithSurroundingText_CompactsJsonOnly()
    {
        var content = """
            ---
            Title: Test
            ---

            Some instructions before JSON.

            ```json
            {
                "response_format": {
                    "type": "json_object"
                }
            }
            ```

            Some instructions after JSON.
            """;

        var result = _parser.Parse(content);

        Assert.Contains("Some instructions before JSON.", result.Body);
        Assert.Contains("""{"response_format":{"type":"json_object"}}""", result.Body);
        Assert.Contains("Some instructions after JSON.", result.Body);
    }
}
