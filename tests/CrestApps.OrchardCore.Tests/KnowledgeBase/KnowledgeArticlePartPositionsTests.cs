using System.Globalization;
using CrestApps.OrchardCore.KnowledgeBase;
using CrestApps.OrchardCore.KnowledgeBase.Migrations;

namespace CrestApps.OrchardCore.Tests.KnowledgeBase;

/// <summary>
/// Orchard Core's content type editor reads each part's position as a whole number; the category's part at "2.5" made
/// the Knowledge Article type's page fail with a FormatException.
/// </summary>
public sealed class KnowledgeArticlePartPositionsTests
{
    [Fact]
    public void EveryPartOfANewType_HasAWholeDistinctPosition()
    {
        var positions = KnowledgeArticlePartPositions.Order.Select(KnowledgeArticlePartPositions.Of).ToList();

        Assert.All(positions, position => int.Parse(position, CultureInfo.InvariantCulture));
        Assert.Equal(positions.Count, positions.Distinct().Count());
        Assert.Equal("3", KnowledgeArticlePartPositions.Of(KnowledgeBaseConstants.ArticleType));
    }

    // Version 2 put the type's own part, with the category field, at 2.5.
    [Fact]
    public void ATypeWithTheCategoryAt2Point5_IsRenumberedInTheSameOrder()
    {
        var positions = KnowledgeArticlePartPositions.Renumber(
        [
            ("TitlePart", "1"),
            (KnowledgeBaseConstants.PartName, "2"),
            ("MarkdownBodyPart", "3"),
            ("AutoroutePart", "4"),
            (KnowledgeBaseConstants.ArticleType, "2.5"),
        ]);

        Assert.Equal("1", positions["TitlePart"]);
        Assert.Equal("2", positions[KnowledgeBaseConstants.PartName]);
        Assert.Equal("3", positions[KnowledgeBaseConstants.ArticleType]);
        Assert.Equal("4", positions["MarkdownBodyPart"]);
        Assert.Equal("5", positions["AutoroutePart"]);
    }

    // A site that went from version 1 to 2 has the category's part and the body at 3: the category comes first.
    [Fact]
    public void TwoPartsInTheSamePlace_KeepTheTypesUsualOrder_AndAnAddedPartStaysLast()
    {
        var positions = KnowledgeArticlePartPositions.Renumber(
        [
            ("TitlePart", "1"),
            (KnowledgeBaseConstants.PartName, "2"),
            ("MarkdownBodyPart", "3"),
            (KnowledgeBaseConstants.ArticleType, "3"),
            ("AutoroutePart", "4"),
            ("SeoMetaPart", "9"),
        ]);

        Assert.Equal(["TitlePart", KnowledgeBaseConstants.PartName, KnowledgeBaseConstants.ArticleType, "MarkdownBodyPart", "AutoroutePart", "SeoMetaPart"], positions.OrderBy(pair => int.Parse(pair.Value, CultureInfo.InvariantCulture)).Select(pair => pair.Key));
    }

    // Positions an administrator set, all whole and distinct, are left alone.
    [Fact]
    public void WholeDistinctPositions_AreLeftAsTheyAre()
        => Assert.Empty(KnowledgeArticlePartPositions.Renumber([("TitlePart", "5"), (KnowledgeBaseConstants.PartName, "1"), ("MarkdownBodyPart", "10")]));
}
