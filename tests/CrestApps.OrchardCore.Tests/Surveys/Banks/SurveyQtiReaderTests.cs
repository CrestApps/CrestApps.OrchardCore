using CrestApps.OrchardCore.Archives;
using CrestApps.OrchardCore.Surveys.Core.Qti;
using CrestApps.OrchardCore.Tests.Learning.Scorm;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.Banks;

public sealed class SurveyQtiReaderTests
{
    [Fact]
    public void APackage30_ListsItsItemsInManifestOrder_WithTheirIdentifiersTitlesAndVersion()
    {
        // Arrange
        using var package = BanksQtiSamples.Package(
            BanksQtiSamples.Manifest30(("second", "items/b.xml"), ("first", "items/a.xml")),
            [("items/a.xml", BanksQtiSamples.Choice30("first", "First item")), ("items/b.xml", BanksQtiSamples.Choice30("second", "Second item"))],
            ("items/images/sign.png", BanksQtiSamples.Png));

        // Act
        var result = QtiReader.Read(package, "bank.zip");

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(["second", "first"], result.Items.Select(item => item.Identifier));
        Assert.Equal(["Second item", "First item"], result.Items.Select(item => item.Title));
        Assert.All(result.Items, item => Assert.Equal("3.0", item.Version));
        Assert.Equal("items/b.xml", result.Items[0].Path);
        Assert.All(result.Items, item => Assert.True(item.Imported, item.Refusal + " " + item.RefusalDetail));
        Assert.All(result.Items, item => Assert.Equal(QtiQuestionTypes.SingleChoice, item.Question.QuestionType));
    }

    [Fact]
    public void APackage21_IsReadAsVersion21()
    {
        // Arrange
        using var package = BanksQtiSamples.Package(
            BanksQtiSamples.Manifest21(("choice", "choice.xml")),
            [("choice.xml", BanksQtiSamples.Choice21())],
            ("images/sign.png", BanksQtiSamples.Png));

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
        var xml = BanksQtiSamples.Choice30().Replace("<p><img src=\"images/sign.png\" alt=\"NEVER LEAVE LUGGAGE UNATTENDED\"/></p>", string.Empty, StringComparison.Ordinal);
        using var file = BanksQtiSamples.Xml(xml);

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
        using var noManifest = PackageZip.Create(("items/a.xml", BanksQtiSamples.Choice30()));
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
        using var package = BanksQtiSamples.Package(BanksQtiSamples.Manifest30(("a", "a.xml")), ("a.xml", BanksQtiSamples.Choice30()), ("../evil.xml", "pwned"));

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
            var item = BanksQtiSamples.Choice30("dtd").Replace("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", entity, StringComparison.Ordinal)
                .Replace("What does it say?", "&xxe;", StringComparison.Ordinal);
            var manifest = BanksQtiSamples.Manifest30(("dtd", "dtd.xml")).Replace("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", "<?xml version=\"1.0\"?><!DOCTYPE manifest [<!ENTITY a \"aaaa\">]>", StringComparison.Ordinal);
            using var itemPackage = BanksQtiSamples.Package(BanksQtiSamples.Manifest30(("dtd", "dtd.xml")), ("dtd.xml", item));
            using var manifestPackage = BanksQtiSamples.Package(manifest, ("dtd.xml", BanksQtiSamples.Choice30("dtd")));

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
    public void AnUnsupportedInteraction21_IsRefused_NamingTheInteraction(string interaction)
    {
        // Arrange
        var item = BanksQtiSamples.Item21("u", "Unsupported", """
            <responseDeclaration identifier="RESPONSE" cardinality="single" baseType="identifier"/>
            """, $"""<{interaction} responseIdentifier="RESPONSE"><prompt>Answer.</prompt></{interaction}>""", $"""<responseProcessing template="{BanksQtiSamples.MatchCorrect21}"/>""");
        using var package = BanksQtiSamples.Package(BanksQtiSamples.Manifest21(("u", "u.xml")), ("u.xml", item));

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
    public void AnUnsupportedInteraction30_IsRefused_NamingTheInteraction(string element, string name)
    {
        // Arrange
        var item = BanksQtiSamples.Item30("u", "Unsupported", """
            <qti-response-declaration identifier="RESPONSE" cardinality="single" base-type="identifier"/>
            """, $"""<div><{element} response-identifier="RESPONSE"/></div>""", $"""<qti-response-processing template="{BanksQtiSamples.MatchCorrect30}"/>""");
        using var file = BanksQtiSamples.Xml(item);

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
        var adaptive = BanksQtiSamples.Choice30("adaptive").Replace("adaptive=\"false\"", "adaptive=\"true\"", StringComparison.Ordinal);
        var template = BanksQtiSamples.Choice21("template").Replace("<responseDeclaration", "<templateDeclaration identifier=\"A\" cardinality=\"single\" baseType=\"integer\"/><responseDeclaration", StringComparison.Ordinal);
        using var first = BanksQtiSamples.Package(BanksQtiSamples.Manifest30(("adaptive", "a.xml")), [("a.xml", adaptive)], ("images/sign.png", BanksQtiSamples.Png));
        using var second = BanksQtiSamples.Package(BanksQtiSamples.Manifest21(("template", "t.xml")), [("t.xml", template)], ("images/sign.png", BanksQtiSamples.Png));

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
        using var package = BanksQtiSamples.Package(BanksQtiSamples.Manifest30(("gone", "items/gone.xml")), ("other.txt", "x"));

        // Act
        var item = Assert.Single(QtiReader.Read(package, "bank.zip").Items);

        // Assert
        Assert.Equal("gone", item.Identifier);
        Assert.Equal(QtiRefusals.MissingFile, item.Refusal);
        Assert.Equal("items/gone.xml", item.RefusalDetail);
    }
}
