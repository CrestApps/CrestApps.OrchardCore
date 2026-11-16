using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Qti;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments.Qti;

public sealed class QtiItemMappingTests
{
    [Fact]
    public void ASingleChoice30_KeepsItsPromptOptionsRightAnswerAndShuffle_AndItsPictureAsAMediaReference()
    {
        // Arrange
        using var package = QtiSamples.Package(QtiSamples.Manifest30(("choice", "items/choice.xml")), [("items/choice.xml", QtiSamples.Choice30())], ("items/images/sign.png", QtiSamples.Png));

        // Act
        var item = Single(package, "bank.zip");

        // Assert
        var question = item.Question;
        Assert.Equal(AssessmentsConstants.QuestionTypes.SingleChoice, question.QuestionType);
        Assert.Contains("<p>Look at the text in the picture.</p>", question.Prompt, StringComparison.Ordinal);
        Assert.Contains("What does it say?", question.Prompt, StringComparison.Ordinal);
        Assert.Contains("src=\"qti-media:items/images/sign.png\"", question.Prompt, StringComparison.Ordinal);
        Assert.Contains("alt=\"NEVER LEAVE LUGGAGE UNATTENDED\"", question.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("qti-", question.Prompt.Replace("qti-media:", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Equal(["You must stay with your luggage at all times.", "Do not let someone else look after your luggage.", "Remember your luggage when you leave."], question.Options.Select(option => option.Text));
        Assert.Equal([true, false, false], question.Options.Select(option => option.Correct));
        Assert.False(question.Shuffle);
        Assert.Equal(1, question.Points);
        var media = Assert.Single(item.Media);
        Assert.Equal("items/images/sign.png", media.Path);
        Assert.Equal(QtiSamples.Png, media.Content);
    }

    [Fact]
    public void AMultipleChoice21WithMapResponse_IsMultipleChoiceWithPartialCredit_AndPointsFromTheMapping()
    {
        // Arrange
        // The choice_multiple.xml example of QTI 2.1: two right options worth 1 each, and wrong options worth -2.
        var item = QtiSamples.Item21("choiceMultiple", "Composition of Water", """
            <responseDeclaration identifier="RESPONSE" cardinality="multiple" baseType="identifier">
              <correctResponse><value>H</value><value>O</value></correctResponse>
              <mapping lowerBound="0" upperBound="2" defaultValue="-2">
                <mapEntry mapKey="H" mappedValue="1"/>
                <mapEntry mapKey="O" mappedValue="1"/>
                <mapEntry mapKey="Cl" mappedValue="-1"/>
              </mapping>
            </responseDeclaration>
            <outcomeDeclaration identifier="SCORE" cardinality="single" baseType="float"/>
            """, """
            <choiceInteraction responseIdentifier="RESPONSE" shuffle="true" maxChoices="0">
              <prompt>Which of the following elements are used to form water?</prompt>
              <simpleChoice identifier="H" fixed="false">Hydrogen</simpleChoice>
              <simpleChoice identifier="He" fixed="false">Helium</simpleChoice>
              <simpleChoice identifier="C" fixed="false">Carbon</simpleChoice>
              <simpleChoice identifier="O" fixed="false">Oxygen</simpleChoice>
              <simpleChoice identifier="N" fixed="false">Nitrogen</simpleChoice>
              <simpleChoice identifier="Cl" fixed="false">Chlorine</simpleChoice>
            </choiceInteraction>
            """, $"""<responseProcessing template="{QtiSamples.MapResponse21}"/>""");

        // Act
        var question = Single(item).Question;

        // Assert
        Assert.Equal(AssessmentsConstants.QuestionTypes.MultipleChoice, question.QuestionType);
        Assert.Equal(["Hydrogen", "Oxygen"], question.Options.Where(option => option.Correct).Select(option => option.Text));
        Assert.Equal(6, question.Options.Count);
        Assert.True(question.Shuffle);
        Assert.True(question.PartialCredit);
        Assert.Equal(2, question.Points);
    }

    [Fact]
    public void ATwoChoiceTrueFalse_IsATrueOrFalseQuestion_AndMaxScoreGivesThePoints()
    {
        // Arrange
        var item = QtiSamples.Item30("tf", "Sky", """
            <qti-response-declaration identifier="RESPONSE" cardinality="single" base-type="identifier">
              <qti-correct-response><qti-value>F</qti-value></qti-correct-response>
            </qti-response-declaration>
            <qti-outcome-declaration identifier="SCORE" cardinality="single" base-type="float"/>
            <qti-outcome-declaration identifier="MAXSCORE" cardinality="single" base-type="float">
              <qti-default-value><qti-value>3</qti-value></qti-default-value>
            </qti-outcome-declaration>
            """, """
            <qti-choice-interaction response-identifier="RESPONSE" max-choices="1">
              <qti-prompt>The sky is green.</qti-prompt>
              <qti-simple-choice identifier="T">True</qti-simple-choice>
              <qti-simple-choice identifier="F">False</qti-simple-choice>
            </qti-choice-interaction>
            """, $"""<qti-response-processing template="{QtiSamples.MatchCorrect30}"/>""");

        // Act
        var question = Single(item).Question;

        // Assert
        Assert.Equal(AssessmentsConstants.QuestionTypes.TrueFalse, question.QuestionType);
        Assert.False(question.TrueFalseAnswer);
        Assert.Equal(3, question.Points);
        Assert.Contains("The sky is green.", question.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void AStandaloneTextEntry_IsAShortTextQuestion_WithTheMappedAnswers()
    {
        // Arrange
        var item = QtiSamples.Item30("capital", "Capital", """
            <qti-response-declaration identifier="RESPONSE" cardinality="single" base-type="string">
              <qti-correct-response><qti-value>Paris</qti-value></qti-correct-response>
              <qti-mapping default-value="0">
                <qti-map-entry map-key="Paris" mapped-value="2" case-sensitive="false"/>
                <qti-map-entry map-key="Lutetia" mapped-value="1" case-sensitive="false"/>
              </qti-mapping>
            </qti-response-declaration>
            <qti-outcome-declaration identifier="SCORE" cardinality="single" base-type="float"/>
            """, """
            <p>What is the capital of France?</p>
            <p><qti-text-entry-interaction response-identifier="RESPONSE" expected-length="15"/></p>
            """, $"""<qti-response-processing template="{QtiSamples.MapResponse30}"/>""");

        // Act
        var question = Single(item).Question;

        // Assert
        Assert.Equal(AssessmentsConstants.QuestionTypes.ShortText, question.QuestionType);
        Assert.Equal(["Paris", "Lutetia"], question.AcceptedAnswers);
        Assert.False(question.CaseSensitive);
        Assert.Equal(2, question.Points);
        Assert.Equal("<p>What is the capital of France?</p>", question.Prompt.Trim());
    }

    [Fact]
    public void AFloatTextEntryWithAToleranceInItsResponseProcessing_IsANumericQuestion()
    {
        // Arrange
        var item = QtiSamples.Item21("gravity", "Gravity", """
            <responseDeclaration identifier="RESPONSE" cardinality="single" baseType="float">
              <correctResponse><value>9.81</value></correctResponse>
            </responseDeclaration>
            <outcomeDeclaration identifier="SCORE" cardinality="single" baseType="float"/>
            """, """
            <p>What is the acceleration of gravity at sea level, in m/s²?</p>
            <p><textEntryInteraction responseIdentifier="RESPONSE"/></p>
            """, """
            <responseProcessing>
              <responseCondition>
                <responseIf>
                  <equal toleranceMode="absolute" tolerance="0.05 0.05">
                    <variable identifier="RESPONSE"/>
                    <correct identifier="RESPONSE"/>
                  </equal>
                  <setOutcomeValue identifier="SCORE"><baseValue baseType="float">1</baseValue></setOutcomeValue>
                </responseIf>
                <responseElse>
                  <setOutcomeValue identifier="SCORE"><baseValue baseType="float">0</baseValue></setOutcomeValue>
                </responseElse>
              </responseCondition>
            </responseProcessing>
            """);

        // Act
        var question = Single(item).Question;

        // Assert
        Assert.Equal(AssessmentsConstants.QuestionTypes.Numeric, question.QuestionType);
        Assert.Equal(9.81m, question.NumericAnswer);
        Assert.Equal(0.05m, question.Tolerance);
    }

    [Fact]
    public void AnExtendedText_IsAnEssay_WithTheScorerRubricAsGraderGuidance()
    {
        // Arrange
        var item = QtiSamples.Item30("essay", "Writing a Postcard", """
            <qti-response-declaration identifier="RESPONSE" cardinality="single" base-type="string"/>
            <qti-outcome-declaration identifier="SCORE" cardinality="single" base-type="float">
              <qti-default-value><qti-value>0</qti-value></qti-default-value>
            </qti-outcome-declaration>
            """, """
            <qti-rubric-block use="scoring" view="scorer"><p>Award full marks for a greeting, a body and a sign-off.</p></qti-rubric-block>
            <p>Write a postcard to a friend about your holiday.</p>
            <qti-extended-text-interaction response-identifier="RESPONSE" expected-lines="5"/>
            """, string.Empty);

        // Act
        var question = Single(item).Question;

        // Assert
        Assert.Equal(AssessmentsConstants.QuestionTypes.Essay, question.QuestionType);
        Assert.Equal("Award full marks for a greeting, a body and a sign-off.", question.GraderGuidance);
        Assert.DoesNotContain("Award full marks", question.Prompt, StringComparison.Ordinal);
        Assert.Contains("Write a postcard", question.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void AMatchInteraction21_IsAMatchingQuestion_WithItsDirectedPairs()
    {
        // Arrange
        var item = QtiSamples.Item21("match", "Capitals", """
            <responseDeclaration identifier="RESPONSE" cardinality="multiple" baseType="directedPair">
              <correctResponse><value>FR PAR</value><value>DE BER</value><value>IT ROM</value></correctResponse>
            </responseDeclaration>
            <outcomeDeclaration identifier="SCORE" cardinality="single" baseType="float"/>
            """, """
            <matchInteraction responseIdentifier="RESPONSE" shuffle="true" maxAssociations="3">
              <prompt>Match each country to its capital.</prompt>
              <simpleMatchSet>
                <simpleAssociableChoice identifier="FR" matchMax="1">France</simpleAssociableChoice>
                <simpleAssociableChoice identifier="DE" matchMax="1">Germany</simpleAssociableChoice>
                <simpleAssociableChoice identifier="IT" matchMax="1">Italy</simpleAssociableChoice>
              </simpleMatchSet>
              <simpleMatchSet>
                <simpleAssociableChoice identifier="BER" matchMax="1">Berlin</simpleAssociableChoice>
                <simpleAssociableChoice identifier="PAR" matchMax="1">Paris</simpleAssociableChoice>
                <simpleAssociableChoice identifier="ROM" matchMax="1">Rome</simpleAssociableChoice>
              </simpleMatchSet>
            </matchInteraction>
            """, $"""<responseProcessing template="{QtiSamples.MatchCorrect21}"/>""");

        // Act
        var question = Single(item).Question;

        // Assert
        Assert.Equal(AssessmentsConstants.QuestionTypes.Matching, question.QuestionType);
        Assert.Equal([("France", "Paris"), ("Germany", "Berlin"), ("Italy", "Rome")], question.Pairs);
        Assert.False(question.PartialCredit);
    }

    [Fact]
    public void AnAssociateInteraction30_IsAMatchingQuestion_WithItsPairs()
    {
        // Arrange
        var item = QtiSamples.Item30("associate", "Shakespearian Rivals", """
            <qti-response-declaration identifier="RESPONSE" cardinality="multiple" base-type="pair">
              <qti-correct-response><qti-value>A P</qti-value><qti-value>C M</qti-value></qti-correct-response>
              <qti-mapping default-value="0">
                <qti-map-entry map-key="A P" mapped-value="2"/>
                <qti-map-entry map-key="C M" mapped-value="1"/>
              </qti-mapping>
            </qti-response-declaration>
            <qti-outcome-declaration identifier="SCORE" cardinality="single" base-type="float"/>
            """, """
            <qti-associate-interaction response-identifier="RESPONSE" shuffle="false" max-associations="2">
              <qti-prompt>Hidden in this list of characters are two pairs of sworn rivals. Pair them up.</qti-prompt>
              <qti-simple-associable-choice identifier="A" match-max="1">Antonio</qti-simple-associable-choice>
              <qti-simple-associable-choice identifier="C" match-max="1">Capulet</qti-simple-associable-choice>
              <qti-simple-associable-choice identifier="M" match-max="1">Montague</qti-simple-associable-choice>
              <qti-simple-associable-choice identifier="P" match-max="1">Prospero</qti-simple-associable-choice>
            </qti-associate-interaction>
            """, $"""<qti-response-processing template="{QtiSamples.MapResponse30}"/>""");

        // Act
        var question = Single(item).Question;

        // Assert
        Assert.Equal(AssessmentsConstants.QuestionTypes.Matching, question.QuestionType);
        Assert.Equal([("Antonio", "Prospero"), ("Capulet", "Montague")], question.Pairs);
        Assert.True(question.PartialCredit);
        Assert.Equal(3, question.Points);
    }

    [Fact]
    public void AnOrderInteraction30_IsAnOrderingQuestion_WithItsItemsInTheRightOrder()
    {
        // Arrange
        var item = QtiSamples.Item30("order", "Grand Prix of Bahrain", """
            <qti-response-declaration identifier="RESPONSE" cardinality="ordered" base-type="identifier">
              <qti-correct-response><qti-value>DriverC</qti-value><qti-value>DriverA</qti-value><qti-value>DriverB</qti-value></qti-correct-response>
            </qti-response-declaration>
            <qti-outcome-declaration identifier="SCORE" cardinality="single" base-type="float"/>
            """, """
            <qti-order-interaction response-identifier="RESPONSE" shuffle="true">
              <qti-prompt>The rules of Formula 1 give 10 points to the winner. Put the drivers in the order they finished.</qti-prompt>
              <qti-simple-choice identifier="DriverA">Rubens Barrichello</qti-simple-choice>
              <qti-simple-choice identifier="DriverB">Jenson Button</qti-simple-choice>
              <qti-simple-choice identifier="DriverC" fixed="true">Michael Schumacher</qti-simple-choice>
            </qti-order-interaction>
            """, $"""<qti-response-processing template="{QtiSamples.MatchCorrect30}"/>""");

        // Act
        var question = Single(item).Question;

        // Assert
        Assert.Equal(AssessmentsConstants.QuestionTypes.Ordering, question.QuestionType);
        Assert.Equal(["Michael Schumacher", "Rubens Barrichello", "Jenson Button"], question.Options.Select(option => option.Text));
    }

    [Fact]
    public void InlineTextEntries21_AreAFillInTheBlanksQuestion_WithEachBlanksAcceptedAnswers()
    {
        // Arrange
        // The text_entry.xml example of QTI 2.1, with a second blank.
        var item = QtiSamples.Item21("textEntry", "Richard III", """
            <responseDeclaration identifier="RESPONSE" cardinality="single" baseType="string">
              <correctResponse><value>York</value></correctResponse>
              <mapping defaultValue="0">
                <mapEntry mapKey="York" mappedValue="1"/>
                <mapEntry mapKey="york" mappedValue="0.5"/>
              </mapping>
            </responseDeclaration>
            <responseDeclaration identifier="RESPONSE2" cardinality="single" baseType="string">
              <correctResponse><value>winter</value></correctResponse>
            </responseDeclaration>
            <outcomeDeclaration identifier="SCORE" cardinality="single" baseType="float"/>
            """, """
            <p>Identify the missing words in this famous quote from Shakespeare's Richard III.</p>
            <blockquote>
              <p>Now is the <textEntryInteraction responseIdentifier="RESPONSE2" expectedLength="10"/> of our discontent<br/>
              Made glorious summer by this sun of <textEntryInteraction responseIdentifier="RESPONSE" expectedLength="15"/>.</p>
            </blockquote>
            """, """
            <responseProcessing>
              <setOutcomeValue identifier="SCORE">
                <sum><mapResponse identifier="RESPONSE"/><mapResponse identifier="RESPONSE2"/></sum>
              </setOutcomeValue>
            </responseProcessing>
            """);

        // Act
        var question = Single(item).Question;

        // Assert
        Assert.Equal(AssessmentsConstants.QuestionTypes.FillInBlanks, question.QuestionType);
        Assert.Equal("Now is the [[winter]] of our discontent Made glorious summer by this sun of [[York|york]].", question.BlankText);
        Assert.Contains("Identify the missing words", question.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("discontent", question.Prompt, StringComparison.Ordinal);
        Assert.True(question.CaseSensitive);
    }

    [Fact]
    public void FeedbackTiedToAChoice_BecomesThatOptionsFeedback_AndASingleOtherFeedbackIsGeneral()
    {
        // Arrange
        var item = QtiSamples.Item21("fb", "Feedback", """
            <responseDeclaration identifier="RESPONSE" cardinality="single" baseType="identifier">
              <correctResponse><value>A</value></correctResponse>
            </responseDeclaration>
            <outcomeDeclaration identifier="SCORE" cardinality="single" baseType="float"/>
            <outcomeDeclaration identifier="FEEDBACK" cardinality="single" baseType="identifier"/>
            """, """
            <choiceInteraction responseIdentifier="RESPONSE" maxChoices="1">
              <prompt>Pick A.</prompt>
              <simpleChoice identifier="A">A<feedbackInline outcomeIdentifier="FEEDBACK" identifier="A" showHide="show">Well done.</feedbackInline></simpleChoice>
              <simpleChoice identifier="B">B</simpleChoice>
            </choiceInteraction>
            """, $"""
            <responseProcessing template="{QtiSamples.MatchCorrect21}"/>
            <modalFeedback outcomeIdentifier="FEEDBACK" identifier="B" showHide="show">B is not right.</modalFeedback>
            <modalFeedback outcomeIdentifier="COMPLETION" identifier="done" showHide="show"><p>Thanks for answering.</p></modalFeedback>
            """);

        // Act
        var question = Single(item).Question;

        // Assert
        Assert.Equal(["A", "B"], question.Options.Select(option => option.Text));
        Assert.Equal(["Well done.", "B is not right."], question.Options.Select(option => option.Feedback));
        Assert.Equal("<p>Thanks for answering.</p>", question.GeneralFeedback.Trim());
    }

    [Fact]
    public void AChoiceWithAPicture_IsRefused_RatherThanImportedWithoutIt()
    {
        // Arrange
        var item = QtiSamples.Item30("pic", "Pictures", """
            <qti-response-declaration identifier="RESPONSE" cardinality="single" base-type="identifier">
              <qti-correct-response><qti-value>A</qti-value></qti-correct-response>
            </qti-response-declaration>
            """, """
            <qti-choice-interaction response-identifier="RESPONSE" max-choices="1">
              <qti-simple-choice identifier="A"><img src="a.png" alt="A"/></qti-simple-choice>
              <qti-simple-choice identifier="B">B</qti-simple-choice>
            </qti-choice-interaction>
            """, $"""<qti-response-processing template="{QtiSamples.MatchCorrect30}"/>""");

        // Act
        var refused = Single(item);

        // Assert
        Assert.Equal(QtiRefusals.MarkupInText, refused.Refusal);
        Assert.Equal("img", refused.RefusalDetail);
        Assert.Null(refused.Question);
    }

    [Theory]
    [InlineData("https://example.com/a.png", QtiRefusals.UnsupportedMedia)]
    [InlineData("images/a.svg", QtiRefusals.UnsupportedMedia)]
    [InlineData("images/missing.png", QtiRefusals.MissingMedia)]
    [InlineData("../outside.png", QtiRefusals.MissingMedia)]
    public void APictureTheImportCannotStore_IsRefused(string source, string refusal)
    {
        // Arrange
        var xml = QtiSamples.Choice30().Replace("images/sign.png", source, StringComparison.Ordinal);
        using var package = QtiSamples.Package(QtiSamples.Manifest30(("choice", "choice.xml")), [("choice.xml", xml), ("images/a.svg", "<svg/>")], ("images/sign.png", QtiSamples.Png));

        // Act
        var item = Single(package, "bank.zip");

        // Assert
        Assert.Equal(refusal, item.Refusal);
        Assert.Equal(source, item.RefusalDetail);
    }

    [Fact]
    public void ACustomResponseProcessing_IsRefused()
    {
        // Arrange
        var xml = QtiSamples.Choice21().Replace($"<responseProcessing template=\"{QtiSamples.MatchCorrect21}\"/>", "<responseProcessing><setOutcomeValue identifier=\"SCORE\"><customOperator class=\"x\"/></setOutcomeValue></responseProcessing>", StringComparison.Ordinal)
            .Replace("<p><img src=\"images/sign.png\" alt=\"NEVER LEAVE LUGGAGE UNATTENDED\"/></p>", string.Empty, StringComparison.Ordinal);

        // Act
        var item = Single(xml);

        // Assert
        Assert.Equal(QtiRefusals.CustomScoring, item.Refusal);
        Assert.Equal("customOperator", item.RefusalDetail);
    }

    private static QtiItem Single(string xml)
    {
        using var file = QtiSamples.Xml(xml);

        return Single(file, "item.xml");
    }

    private static QtiItem Single(Stream file, string name)
    {
        var result = QtiReader.Read(file, name);

        Assert.True(result.Succeeded, result.Error + " " + result.ErrorDetail);

        var item = Assert.Single(result.Items);

        return item;
    }
}
