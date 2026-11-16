using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Qti;
using CrestApps.OrchardCore.Learning.Core.Archives;
using CrestApps.OrchardCore.Tests.Learning.Scorm;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments.Qti;

public sealed class QtiReaderTests
{
    [Fact]
    public void APackage30_ListsItsItemsInManifestOrder_WithTheirIdentifiersTitlesAndVersion()
    {
        // Arrange
        using var package = QtiSamples.Package(
            QtiSamples.Manifest30(("second", "items/b.xml"), ("first", "items/a.xml")),
            [("items/a.xml", QtiSamples.Choice30("first", "First item")), ("items/b.xml", QtiSamples.Choice30("second", "Second item"))],
            ("items/images/sign.png", QtiSamples.Png));

        // Act
        var result = QtiReader.Read(package, "bank.zip");

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(["second", "first"], result.Items.Select(item => item.Identifier));
        Assert.Equal(["Second item", "First item"], result.Items.Select(item => item.Title));
        Assert.All(result.Items, item => Assert.Equal("3.0", item.Version));
        Assert.Equal("items/b.xml", result.Items[0].Path);
        Assert.All(result.Items, item => Assert.True(item.Imported, item.Refusal + " " + item.RefusalDetail));
        Assert.All(result.Items, item => Assert.Equal(AssessmentsConstants.QuestionTypes.SingleChoice, item.Question.QuestionType));
    }

    [Fact]
    public void APackage21_IsReadAsVersion21()
    {
        // Arrange
        using var package = QtiSamples.Package(
            QtiSamples.Manifest21(("choice", "choice.xml")),
            [("choice.xml", QtiSamples.Choice21())],
            ("images/sign.png", QtiSamples.Png));

        // Act
        var result = QtiReader.Read(package, "bank.zip");

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("2.1", item.Version);
        Assert.Equal("choice", item.Identifier);
        Assert.True(item.Imported, item.Refusal + " " + item.RefusalDetail);
    }

    [Fact]
    public void ASingleItemFile_IsReadAsOneItem()
    {
        // Arrange
        var xml = QtiSamples.Choice30().Replace("<p><img src=\"images/sign.png\" alt=\"NEVER LEAVE LUGGAGE UNATTENDED\"/></p>", string.Empty, StringComparison.Ordinal);
        using var file = QtiSamples.Xml(xml);

        // Act
        var result = QtiReader.Read(file, "luggage.xml");

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("choice", item.Identifier);
        Assert.Equal("luggage.xml", item.Path);
        Assert.True(item.Imported, item.Refusal + " " + item.RefusalDetail);
    }

    [Fact]
    public void AZipThatIsNotAQtiPackage_IsRefused()
    {
        // Arrange
        using var noManifest = PackageZip.Create(("items/a.xml", QtiSamples.Choice30()));
        using var scorm = PackageZip.SingleScoPackage();

        // Act
        var first = QtiReader.Read(noManifest, "a.zip");
        var second = QtiReader.Read(scorm, "course.zip");

        // Assert
        Assert.Equal(QtiErrors.NoManifest, first.Error);
        Assert.Equal(QtiErrors.NoItems, second.Error);
    }

    [Fact]
    public void AnUnsafeArchive_IsRefusedWithTheRuleAndTheEntry()
    {
        // Arrange
        using var package = QtiSamples.Package(QtiSamples.Manifest30(("a", "a.xml")), ("a.xml", QtiSamples.Choice30()), ("../evil.xml", "pwned"));

        // Act
        var result = QtiReader.Read(package, "bank.zip");

        // Assert
        Assert.Equal(QtiErrors.UnsafeArchive, result.Error);
        Assert.Contains(PackageArchiveErrors.UnsafePath, result.ErrorDetail, StringComparison.Ordinal);
        Assert.Contains("../evil.xml", result.ErrorDetail, StringComparison.Ordinal);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void ADtdOrAnEntity_IsRefused_AndNoExternalFileIsRead()
    {
        // Arrange
        var secret = Path.Combine(Path.GetTempPath(), "qti-secret-" + Guid.NewGuid().ToString("n") + ".txt");
        File.WriteAllText(secret, "TOP SECRET");

        try
        {
            var entity = $"""<?xml version="1.0"?><!DOCTYPE qti-assessment-item [<!ENTITY xxe SYSTEM "file:///{secret.Replace('\\', '/')}">]>""";
            var item = QtiSamples.Choice30("dtd").Replace("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", entity, StringComparison.Ordinal)
                .Replace("What does it say?", "&xxe;", StringComparison.Ordinal);
            var manifest = QtiSamples.Manifest30(("dtd", "dtd.xml")).Replace("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", "<?xml version=\"1.0\"?><!DOCTYPE manifest [<!ENTITY a \"aaaa\">]>", StringComparison.Ordinal);
            using var itemPackage = QtiSamples.Package(QtiSamples.Manifest30(("dtd", "dtd.xml")), ("dtd.xml", item));
            using var manifestPackage = QtiSamples.Package(manifest, ("dtd.xml", QtiSamples.Choice30("dtd")));

            // Act
            var itemResult = QtiReader.Read(itemPackage, "bank.zip");
            var manifestResult = QtiReader.Read(manifestPackage, "bank.zip");

            // Assert
            var refused = Assert.Single(itemResult.Items);
            Assert.Equal(QtiRefusals.InvalidXml, refused.Refusal);
            Assert.Null(refused.Question);
            Assert.Equal(QtiErrors.InvalidManifest, manifestResult.Error);
        }
        finally
        {
            File.Delete(secret);
        }
    }

    [Theory]
    [InlineData("hotspotInteraction")]
    [InlineData("graphicOrderInteraction")]
    [InlineData("selectPointInteraction")]
    [InlineData("sliderInteraction")]
    [InlineData("drawingInteraction")]
    [InlineData("uploadInteraction")]
    [InlineData("customInteraction")]
    [InlineData("mediaInteraction")]
    [InlineData("hottextInteraction")]
    [InlineData("gapMatchInteraction")]
    public void AnUnsupportedInteraction21_IsRefused_NamingTheInteraction(string interaction)
    {
        // Arrange
        var item = QtiSamples.Item21("u", "Unsupported", """
            <responseDeclaration identifier="RESPONSE" cardinality="single" baseType="identifier"/>
            """, $"""<{interaction} responseIdentifier="RESPONSE"><prompt>Answer.</prompt></{interaction}>""", $"""<responseProcessing template="{QtiSamples.MatchCorrect21}"/>""");
        using var package = QtiSamples.Package(QtiSamples.Manifest21(("u", "u.xml")), ("u.xml", item));

        // Act
        var result = QtiReader.Read(package, "bank.zip");

        // Assert
        var refused = Assert.Single(result.Items);
        Assert.Equal(QtiRefusals.UnsupportedInteraction, refused.Refusal);
        Assert.Equal(interaction, refused.RefusalDetail);
    }

    [Theory]
    [InlineData("qti-portable-custom-interaction", "portableCustomInteraction")]
    [InlineData("qti-graphic-gap-match-interaction", "graphicGapMatchInteraction")]
    [InlineData("qti-end-attempt-interaction", "endAttemptInteraction")]
    [InlineData("qti-inline-choice-interaction", "inlineChoiceInteraction")]
    public void AnUnsupportedInteraction30_IsRefused_NamingTheInteraction(string element, string name)
    {
        // Arrange
        var item = QtiSamples.Item30("u", "Unsupported", """
            <qti-response-declaration identifier="RESPONSE" cardinality="single" base-type="identifier"/>
            """, $"""<div><{element} response-identifier="RESPONSE"/></div>""", $"""<qti-response-processing template="{QtiSamples.MatchCorrect30}"/>""");
        using var file = QtiSamples.Xml(item);

        // Act
        var result = QtiReader.Read(file, "u.xml");

        // Assert
        var refused = Assert.Single(result.Items);
        Assert.Equal(QtiRefusals.UnsupportedInteraction, refused.Refusal);
        Assert.Equal(name, refused.RefusalDetail);
    }

    [Fact]
    public void AnAdaptiveOrTemplateItem_IsRefused()
    {
        // Arrange
        var adaptive = QtiSamples.Choice30("adaptive").Replace("adaptive=\"false\"", "adaptive=\"true\"", StringComparison.Ordinal);
        var template = QtiSamples.Choice21("template").Replace("<responseDeclaration", "<templateDeclaration identifier=\"A\" cardinality=\"single\" baseType=\"integer\"/><responseDeclaration", StringComparison.Ordinal);
        using var first = QtiSamples.Package(QtiSamples.Manifest30(("adaptive", "a.xml")), [("a.xml", adaptive)], ("images/sign.png", QtiSamples.Png));
        using var second = QtiSamples.Package(QtiSamples.Manifest21(("template", "t.xml")), [("t.xml", template)], ("images/sign.png", QtiSamples.Png));

        // Act
        var adaptiveItem = Assert.Single(QtiReader.Read(first, "a.zip").Items);
        var templateItem = Assert.Single(QtiReader.Read(second, "t.zip").Items);

        // Assert
        Assert.Equal(QtiRefusals.Adaptive, adaptiveItem.Refusal);
        Assert.Equal(QtiRefusals.Template, templateItem.Refusal);
    }

    [Fact]
    public void AnItemTheManifestNamesButThePackageLacks_IsRefused()
    {
        // Arrange
        using var package = QtiSamples.Package(QtiSamples.Manifest30(("gone", "items/gone.xml")), ("other.txt", "x"));

        // Act
        var item = Assert.Single(QtiReader.Read(package, "bank.zip").Items);

        // Assert
        Assert.Equal("gone", item.Identifier);
        Assert.Equal(QtiRefusals.MissingFile, item.Refusal);
        Assert.Equal("items/gone.xml", item.RefusalDetail);
    }
}
