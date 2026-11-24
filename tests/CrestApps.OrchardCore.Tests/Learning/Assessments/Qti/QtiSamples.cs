using System.Text;
using CrestApps.OrchardCore.Tests.Learning.Scorm;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments.Qti;

/// <summary>
/// QTI 3.0 and QTI 2.1 test files, written from the examples of the specifications (the QTI 3.0 Assessment, Section
/// and Item information model and its implementation guide; the QTI 2.1 information model and its examples).
/// </summary>
internal static class QtiSamples
{
    public const string Namespace30 = "http://www.imsglobal.org/xsd/imsqtiasi_v3p0";

    public const string Namespace21 = "http://www.imsglobal.org/xsd/imsqti_v2p1";

    public const string MatchCorrect30 = "https://purl.imsglobal.org/spec/qti/v3p0/rptemplates/match_correct.xml";

    public const string MapResponse30 = "https://purl.imsglobal.org/spec/qti/v3p0/rptemplates/map_response.xml";

    public const string MatchCorrect21 = "http://www.imsglobal.org/question/qti_v2p1/rptemplates/match_correct";

    public const string MapResponse21 = "http://www.imsglobal.org/question/qti_v2p1/rptemplates/map_response";

    /// <summary>
    /// The single choice item of the QTI 3.0 implementation guide, with a picture.
    /// </summary>
    public static string Choice30(string identifier = "choice", string title = "Unattended Luggage")
        => Item30(identifier, title, """
            <qti-response-declaration identifier="RESPONSE" cardinality="single" base-type="identifier">
              <qti-correct-response><qti-value>ChoiceA</qti-value></qti-correct-response>
            </qti-response-declaration>
            <qti-outcome-declaration identifier="SCORE" cardinality="single" base-type="float">
              <qti-default-value><qti-value>0</qti-value></qti-default-value>
            </qti-outcome-declaration>
            """, """
            <p>Look at the text in the picture.</p>
            <p><img src="images/sign.png" alt="NEVER LEAVE LUGGAGE UNATTENDED"/></p>
            <qti-choice-interaction response-identifier="RESPONSE" shuffle="false" max-choices="1">
              <qti-prompt>What does it say?</qti-prompt>
              <qti-simple-choice identifier="ChoiceA">You must stay with your luggage at all times.</qti-simple-choice>
              <qti-simple-choice identifier="ChoiceB">Do not let someone else look after your luggage.</qti-simple-choice>
              <qti-simple-choice identifier="ChoiceC">Remember your luggage when you leave.</qti-simple-choice>
            </qti-choice-interaction>
            """, $"""<qti-response-processing template="{MatchCorrect30}"/>""");

    /// <summary>
    /// The single choice item of the QTI 2.1 examples (choice.xml).
    /// </summary>
    public static string Choice21(string identifier = "choice", string title = "Unattended Luggage")
        => Item21(identifier, title, """
            <responseDeclaration identifier="RESPONSE" cardinality="single" baseType="identifier">
              <correctResponse><value>ChoiceA</value></correctResponse>
            </responseDeclaration>
            <outcomeDeclaration identifier="SCORE" cardinality="single" baseType="float">
              <defaultValue><value>0</value></defaultValue>
            </outcomeDeclaration>
            """, """
            <p>Look at the text in the picture.</p>
            <p><img src="images/sign.png" alt="NEVER LEAVE LUGGAGE UNATTENDED"/></p>
            <choiceInteraction responseIdentifier="RESPONSE" shuffle="false" maxChoices="1">
              <prompt>What does it say?</prompt>
              <simpleChoice identifier="ChoiceA">You must stay with your luggage at all times.</simpleChoice>
              <simpleChoice identifier="ChoiceB">Do not let someone else look after your luggage.</simpleChoice>
              <simpleChoice identifier="ChoiceC">Remember your luggage when you leave.</simpleChoice>
            </choiceInteraction>
            """, $"""<responseProcessing template="{MatchCorrect21}"/>""");

    public static string Item30(string identifier, string title, string declarations, string body, string processing, string attributes = "")
        => $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <qti-assessment-item xmlns="{Namespace30}" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                xsi:schemaLocation="{Namespace30} https://purl.imsglobal.org/spec/qti/v3p0/schema/xsd/imsqti_asiv3p0_v1p0.xsd"
                identifier="{identifier}" title="{title}" adaptive="false" time-dependent="false" xml:lang="en" {attributes}>
              {declarations}
              <qti-item-body>
              {body}
              </qti-item-body>
              {processing}
            </qti-assessment-item>
            """;

    public static string Item21(string identifier, string title, string declarations, string body, string processing, string attributes = "")
        => $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <assessmentItem xmlns="{Namespace21}" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                xsi:schemaLocation="{Namespace21} http://www.imsglobal.org/xsd/qti/qtiv2p1/imsqti_v2p1.xsd"
                identifier="{identifier}" title="{title}" adaptive="false" timeDependent="false" {attributes}>
              {declarations}
              <itemBody>
              {body}
              </itemBody>
              {processing}
            </assessmentItem>
            """;

    /// <summary>
    /// A QTI 3.0 content package manifest listing items.
    /// </summary>
    public static string Manifest30(params (string Identifier, string Href)[] items)
        => Manifest("http://www.imsglobal.org/xsd/qti/qtiv3p0/imscp_v1p1", "QTI Package", "3.0.0", "imsqti_item_xmlv3p0", items);

    /// <summary>
    /// A QTI 2.1 content package manifest listing items.
    /// </summary>
    public static string Manifest21(params (string Identifier, string Href)[] items)
        => Manifest("http://www.imsglobal.org/xsd/imscp_v1p1", "QTIv2.1 Package", "1.0.0", "imsqti_item_xmlv2p1", items);

    public static string Manifest(string ns, string schema, string version, string type, (string Identifier, string Href)[] items)
    {
        var resources = new StringBuilder();

        foreach (var (identifier, href) in items)
        {
            resources.Append($"""
                    <resource identifier="{identifier}" type="{type}" href="{href}">
                      <file href="{href}"/>
                    </resource>
                """);
        }

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <manifest xmlns="{ns}" identifier="MANIFEST-1">
              <metadata>
                <schema>{schema}</schema>
                <schemaversion>{version}</schemaversion>
              </metadata>
              <organizations/>
              <resources>
            {resources}
              </resources>
            </manifest>
            """;
    }

    /// <summary>
    /// A small PNG image (1 by 1 pixel).
    /// </summary>
    public static byte[] Png { get; } = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    public static MemoryStream Package(string manifest, params (string Name, string Content)[] files)
        => PackageZip.Create([("imsmanifest.xml", manifest.TrimStart()), .. files.Select(file => (file.Name, file.Content.TrimStart()))]);

    public static MemoryStream Package(string manifest, (string Name, string Content)[] files, params (string Name, byte[] Content)[] binaries)
        => PackageZip.Create(
        [
            ("imsmanifest.xml", Encoding.UTF8.GetBytes(manifest.TrimStart()), 0),
            .. files.Select(file => (file.Name, Encoding.UTF8.GetBytes(file.Content.TrimStart()), 0)),
            .. binaries.Select(file => (file.Name, file.Content, 0)),
        ]);

    public static MemoryStream Xml(string content)
        => new(Encoding.UTF8.GetBytes(content.TrimStart()));
}
