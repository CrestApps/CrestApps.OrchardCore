using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using OrchardCore.ContentManagement;
using YesSql.Filters.Nodes;
using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

/// <summary>
/// The free words of a contact or lead list search are read as one phrase. Joined with "or", each word was matched
/// through its own join of the content item index, which no index can answer, and a name search on a large tenant
/// outlived the command timeout.
/// </summary>
public sealed class OmnichannelContactListFilterParserTests
{
    [Theory]
    [InlineData("John Smith", "\"John Smith\"")]
    [InlineData("  John   Smith  ", "\"John Smith\"")]
    [InlineData("converted:false John Smith", "converted:false \"John Smith\"")]
    [InlineData("John converted:false Smith", "converted:false \"John Smith\"")]
    [InlineData("250 818 3760", "\"250 818 3760\"")]
    [InlineData("(702) 555-0104", "\"(702) 555-0104\"")]
    [InlineData("  (702)555-0104 ", "\"(702)555-0104\"")]
    [InlineData("+1 (702) 555 0104", "\"+1 (702) 555 0104\"")]
    public void ToPhraseSearch_JoinsTheFreeWordsIntoOnePhraseAfterTheNamedTerms(string text, string expected)
        => Assert.Equal(expected, OmnichannelContactListFilterParser.ToPhraseSearch(text));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Smith")]
    [InlineData("converted:false Smith")]
    [InlineData("converted:false status:Published")]
    [InlineData("\"John Smith\" Jones")]
    [InlineData("John OR Smith")]
    [InlineData("John and Smith")]
    [InlineData("John NOT Smith")]
    [InlineData("John -Smith")]
    [InlineData("(John Smith)")]
    [InlineData("702")]
    [InlineData("702-555-0104")]
    [InlineData("(702) OR (310)")]
    public void ToPhraseSearch_LeavesASearchWithFewerThanTwoFreeWordsOrItsOwnSyntaxAsTyped(string text)
        => Assert.Equal(text, OmnichannelContactListFilterParser.ToPhraseSearch(text));

    [Fact]
    public void ToPhraseSearch_LeavesANullSearchAsItIs()
        => Assert.Null(OmnichannelContactListFilterParser.ToPhraseSearch(null));

    [Fact]
    public void ANumberWithParentheses_IsParsedAsOneFreeTextTermHoldingTheWholeNumber()
    {
        // Unquoted, the parentheses are the search syntax's grouping, and "(702) 555-0104" became "702" or "555-0104".
        var builder = new QueryEngineBuilder<ContentItem>();
        builder.WithDefaultTerm("text", term => term.ManyCondition((value, query) => query, (value, query) => query));
        var parser = builder.Build();

        var term = Assert.Single(parser.Parse(OmnichannelContactListFilterParser.ToPhraseSearch("(702) 555-0104")));

        var operation = Assert.IsType<UnaryNode>(Assert.IsAssignableFrom<TermOperationNode>(term).Operation);
        Assert.Equal("(702) 555-0104", operation.Value);
    }

    [Fact]
    public void QuotedPhrase_IsParsedAsOneFreeTextTermHoldingTheWholePhrase()
    {
        // The phrase only helps if the search parser reads it as one value; two words unquoted are two values.
        var builder = new QueryEngineBuilder<ContentItem>();
        builder.WithDefaultTerm("text", term => term.ManyCondition((value, query) => query, (value, query) => query));
        var parser = builder.Build();

        var phrase = Assert.Single(parser.Parse(OmnichannelContactListFilterParser.ToPhraseSearch("John Smith")));
        var words = parser.Parse("John Smith").ToArray();

        var phraseOperation = Assert.IsType<UnaryNode>(Assert.IsAssignableFrom<TermOperationNode>(phrase).Operation);
        Assert.Equal("John Smith", phraseOperation.Value);
        Assert.False(
            words.Length == 1 && words[0] is TermOperationNode { Operation: UnaryNode },
            "Unquoted, the two words are expected to be two values; if they are one, the phrase is not needed.");
    }
}
