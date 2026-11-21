using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Qti;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments.Qti;

/// <summary>
/// QTI gap match becomes a drag into text question, inline choices a gapselect question, and an item with no interaction
/// a description.
/// </summary>
public sealed class QtiMoodleTypesTests
{
    [Fact]
    public void AGapMatch21_IsDragIntoText_WithTheUnusedWordsAsDistractors()
    {
        // Arrange
        // The gap_match.xml example of QTI 2.1.
        var xml = QtiSamples.Item21("gapMatch", "Richard III", """
            <responseDeclaration identifier="RESPONSE" cardinality="multiple" baseType="directedPair">
              <correctResponse><value>W G1</value><value>Su G2</value></correctResponse>
            </responseDeclaration>
            <outcomeDeclaration identifier="SCORE" cardinality="single" baseType="float"/>
            """, """
            <gapMatchInteraction responseIdentifier="RESPONSE" shuffle="false">
              <prompt>Identify the missing words in this famous quote from Shakespeare's Richard III.</prompt>
              <gapText identifier="W" matchMax="1">winter</gapText>
              <gapText identifier="Sp" matchMax="1">spring</gapText>
              <gapText identifier="Su" matchMax="1">summer</gapText>
              <gapText identifier="A" matchMax="1">autumn</gapText>
              <blockquote><p>Now is the <gap identifier="G1"/> of our discontent<br/> Made glorious <gap identifier="G2"/> by this sun of York;</p></blockquote>
            </gapMatchInteraction>
            """, $"""<responseProcessing template="{QtiSamples.MatchCorrect21}"/>""");

        // Act
        var question = Read(xml).Question;

        // Assert
        Assert.Equal(AssessmentsConstants.QuestionTypes.DragIntoText, question.QuestionType);
        Assert.Equal("Now is the [[winter]] of our discontent Made glorious [[summer]] by this sun of York;", question.BlankText);
        Assert.Equal(["spring", "autumn"], question.Distractors);
        Assert.Contains("Identify the missing words", question.Prompt, StringComparison.Ordinal);
        Assert.False(question.Shuffle);
        Assert.Equal(1, question.Points);
    }

    [Fact]
    public void InlineChoices30_AreGapSelect_WithTheWrongChoicesAsDistractors()
    {
        // Arrange
        var xml = QtiSamples.Item30("inline", "Capitals", """
            <qti-response-declaration identifier="R1" cardinality="single" base-type="identifier">
              <qti-correct-response><qti-value>S</qti-value></qti-correct-response>
            </qti-response-declaration>
            <qti-response-declaration identifier="R2" cardinality="single" base-type="identifier">
              <qti-correct-response><qti-value>F</qti-value></qti-correct-response>
            </qti-response-declaration>
            <qti-outcome-declaration identifier="SCORE" cardinality="single" base-type="float"/>
            """, """
            <p>Choose the countries.</p>
            <p>Edinburgh is in <qti-inline-choice-interaction response-identifier="R1" shuffle="true"><qti-inline-choice identifier="S">Scotland</qti-inline-choice><qti-inline-choice identifier="E">England</qti-inline-choice></qti-inline-choice-interaction> and Paris is in <qti-inline-choice-interaction response-identifier="R2"><qti-inline-choice identifier="F">France</qti-inline-choice><qti-inline-choice identifier="W">Wales</qti-inline-choice></qti-inline-choice-interaction>.</p>
            """, $"""<qti-response-processing template="{QtiSamples.MatchCorrect30}"/>""");

        // Act
        var question = Read(xml).Question;

        // Assert
        Assert.Equal(AssessmentsConstants.QuestionTypes.GapSelect, question.QuestionType);
        Assert.Equal("Edinburgh is in [[Scotland]] and Paris is in [[France]].", question.BlankText);
        Assert.Equal(["England", "Wales"], question.Distractors);
        Assert.Contains("Choose the countries.", question.Prompt, StringComparison.Ordinal);
        Assert.True(question.Shuffle);
        Assert.Equal(2, question.Points);
    }

    [Fact]
    public void AnItemWithoutAnInteraction_IsADescription_WorthNoPoints()
    {
        // Arrange
        var xml = QtiSamples.Item21("intro", "Before you start", string.Empty, "<p>Read the safety rules before answering.</p>", string.Empty);

        // Act
        var question = Read(xml).Question;

        // Assert
        Assert.Equal(AssessmentsConstants.QuestionTypes.Description, question.QuestionType);
        Assert.Contains("Read the safety rules", question.Prompt, StringComparison.Ordinal);
        Assert.Equal(0, question.Points);
    }

    private static QtiItem Read(string xml)
    {
        using var file = QtiSamples.Xml(xml);
        var result = QtiReader.Read(file, "item.xml");

        Assert.True(result.Succeeded, result.Error + " " + result.ErrorDetail);
        var item = Assert.Single(result.Items);
        Assert.True(item.Imported, item.Refusal + " " + item.RefusalDetail);

        return item;
    }
}
