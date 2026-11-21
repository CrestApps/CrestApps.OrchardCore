using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Qti;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments.Qti;

/// <summary>
/// Review round 9, M10: scoring the question types cannot express is refused with a reason in the import report, not
/// imported with other grading: unequal or negative mapped values, a single choice with several right answers, and
/// inline response processing beyond the standard shapes.
/// </summary>
public sealed class QtiScoringRound9Tests
{
    [Fact]
    public void Round9M10_AShortTextWithAnswersWorthDifferentPoints_IsRefused()
    {
        var item = Read(QtiSamples.Item21("capital", "Capital", """
            <responseDeclaration identifier="RESPONSE" cardinality="single" baseType="string">
              <correctResponse><value>Paris</value></correctResponse>
              <mapping defaultValue="0">
                <mapEntry mapKey="Paris" mappedValue="2"/>
                <mapEntry mapKey="Lutetia" mappedValue="1"/>
              </mapping>
            </responseDeclaration>
            <outcomeDeclaration identifier="SCORE" cardinality="single" baseType="float"/>
            """, """<p>What is the capital of France?</p><p><textEntryInteraction responseIdentifier="RESPONSE"/></p>""", $"""<responseProcessing template="{QtiSamples.MapResponse21}"/>"""));

        Assert.Equal(QtiRefusals.UnsupportedScoring, item.Refusal);
    }

    [Fact]
    public void Round9M10_AMultipleChoiceWithPenalties_IsRefused()
    {
        var item = Read(QtiSamples.Item21("water", "Water", """
            <responseDeclaration identifier="RESPONSE" cardinality="multiple" baseType="identifier">
              <correctResponse><value>H</value><value>O</value></correctResponse>
              <mapping lowerBound="0" upperBound="2" defaultValue="0">
                <mapEntry mapKey="H" mappedValue="1"/>
                <mapEntry mapKey="O" mappedValue="1"/>
                <mapEntry mapKey="Cl" mappedValue="-1"/>
              </mapping>
            </responseDeclaration>
            <outcomeDeclaration identifier="SCORE" cardinality="single" baseType="float"/>
            """, """
            <choiceInteraction responseIdentifier="RESPONSE" maxChoices="0">
              <prompt>Which elements form water?</prompt>
              <simpleChoice identifier="H">Hydrogen</simpleChoice>
              <simpleChoice identifier="O">Oxygen</simpleChoice>
              <simpleChoice identifier="Cl">Chlorine</simpleChoice>
            </choiceInteraction>
            """, $"""<responseProcessing template="{QtiSamples.MapResponse21}"/>"""));

        Assert.Equal(QtiRefusals.UnsupportedScoring, item.Refusal);
    }

    [Fact]
    public void Round9M10_ASingleChoiceWithTwoRightAnswers_IsRefused_NotMadeAMultipleChoice()
    {
        var item = Read(QtiSamples.Item21("either", "Either", """
            <responseDeclaration identifier="RESPONSE" cardinality="single" baseType="identifier">
              <mapping defaultValue="0">
                <mapEntry mapKey="A" mappedValue="1"/>
                <mapEntry mapKey="B" mappedValue="1"/>
              </mapping>
            </responseDeclaration>
            <outcomeDeclaration identifier="SCORE" cardinality="single" baseType="float"/>
            """, """
            <choiceInteraction responseIdentifier="RESPONSE" maxChoices="1">
              <prompt>Pick one.</prompt>
              <simpleChoice identifier="A">Alpha</simpleChoice>
              <simpleChoice identifier="B">Beta</simpleChoice>
              <simpleChoice identifier="C">Gamma</simpleChoice>
            </choiceInteraction>
            """, $"""<responseProcessing template="{QtiSamples.MapResponse21}"/>"""));

        Assert.Equal(QtiRefusals.UnsupportedScoring, item.Refusal);
    }

    [Fact]
    public void Round9M10_InlineRulesBeyondTheStandardShapes_AreRefused()
    {
        // Half the points for an answer at least as large: known elements, but not a standard template.
        var item = Read(QtiSamples.Item21("estimate", "Estimate", """
            <responseDeclaration identifier="RESPONSE" cardinality="single" baseType="float">
              <correctResponse><value>100</value></correctResponse>
            </responseDeclaration>
            <outcomeDeclaration identifier="SCORE" cardinality="single" baseType="float"/>
            """, """<p>Estimate.</p><p><textEntryInteraction responseIdentifier="RESPONSE"/></p>""", """
            <responseProcessing>
              <responseCondition>
                <responseIf>
                  <match><variable identifier="RESPONSE"/><correct identifier="RESPONSE"/></match>
                  <setOutcomeValue identifier="SCORE"><baseValue baseType="float">1</baseValue></setOutcomeValue>
                </responseIf>
                <responseElseIf>
                  <gte><variable identifier="RESPONSE"/><baseValue baseType="float">90</baseValue></gte>
                  <setOutcomeValue identifier="SCORE"><baseValue baseType="float">0.5</baseValue></setOutcomeValue>
                </responseElseIf>
                <responseElse>
                  <setOutcomeValue identifier="SCORE"><baseValue baseType="float">0</baseValue></setOutcomeValue>
                </responseElse>
              </responseCondition>
            </responseProcessing>
            """));

        Assert.Equal(QtiRefusals.CustomScoring, item.Refusal);
    }

    [Fact]
    public void Round9M10_AMappedShortTextWithEqualValues_IsStillImported()
    {
        var item = Read(QtiSamples.Item21("capital", "Capital", """
            <responseDeclaration identifier="RESPONSE" cardinality="single" baseType="string">
              <correctResponse><value>Paris</value></correctResponse>
              <mapping defaultValue="0">
                <mapEntry mapKey="Paris" mappedValue="2"/>
                <mapEntry mapKey="paris" mappedValue="2"/>
              </mapping>
            </responseDeclaration>
            <outcomeDeclaration identifier="SCORE" cardinality="single" baseType="float"/>
            """, """<p>What is the capital of France?</p><p><textEntryInteraction responseIdentifier="RESPONSE"/></p>""", $"""<responseProcessing template="{QtiSamples.MapResponse21}"/>"""));

        Assert.True(item.Imported, item.Refusal + " " + item.RefusalDetail);
        Assert.Equal(AssessmentsConstants.QuestionTypes.ShortText, item.Question.QuestionType);
        Assert.Equal(2, item.Question.Points);
    }

    private static QtiItem Read(string xml)
    {
        using var file = QtiSamples.Xml(xml);
        var result = QtiReader.Read(file, "item.xml");

        Assert.True(result.Succeeded, result.Error + " " + result.ErrorDetail);

        return Assert.Single(result.Items);
    }
}
