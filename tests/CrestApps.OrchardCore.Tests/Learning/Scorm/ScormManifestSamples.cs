namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// Manifests written from the SCORM 1.2 Content Aggregation Model examples: a single SCO, several SCOs in nested
/// items, assets only, <c>xml:base</c> at three levels, a mastery score and the other <c>adlcp</c> item elements, and an
/// organizations element without a default.
/// </summary>
internal static class ScormManifestSamples
{
    public const string SingleSco = """
        <?xml version="1.0" encoding="UTF-8"?>
        <manifest identifier="com.example.safety" version="1.0"
                  xmlns="http://www.imsproject.org/xsd/imscp_rootv1p1p2"
                  xmlns:adlcp="http://www.adlnet.org/xsd/adlcp_rootv1p2"
                  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                  xsi:schemaLocation="http://www.imsproject.org/xsd/imscp_rootv1p1p2 imscp_rootv1p1p2.xsd http://www.imsglobal.org/xsd/imsmd_rootv1p2p1 imsmd_rootv1p2p1.xsd http://www.adlnet.org/xsd/adlcp_rootv1p2 adlcp_rootv1p2.xsd">
          <metadata>
            <schema>ADL SCORM</schema>
            <schemaversion>1.2</schemaversion>
          </metadata>
          <organizations default="ORG-1">
            <organization identifier="ORG-1">
              <title>Ladder Safety</title>
              <item identifier="ITEM-1" identifierref="RES-1" isvisible="true">
                <title>Ladder Safety Lesson</title>
              </item>
            </organization>
          </organizations>
          <resources>
            <resource identifier="RES-1" type="webcontent" adlcp:scormtype="sco" href="index.html">
              <file href="index.html"/>
              <file href="scripts/lesson.js"/>
            </resource>
          </resources>
        </manifest>
        """;

    public const string MultiSco = """
        <?xml version="1.0" encoding="UTF-8"?>
        <manifest identifier="MULTI" version="1.1"
                  xmlns="http://www.imsproject.org/xsd/imscp_rootv1p1p2"
                  xmlns:adlcp="http://www.adlnet.org/xsd/adlcp_rootv1p2">
          <metadata>
            <schema>ADL SCORM</schema>
            <schemaversion>1.2</schemaversion>
          </metadata>
          <organizations default="TOC1">
            <organization identifier="TOC1">
              <title>Forklift Operator Course</title>
              <item identifier="MODULE-1">
                <title>Module 1</title>
                <item identifier="INTRO" identifierref="R-GLOSSARY">
                  <title>Glossary</title>
                </item>
                <item identifier="LESSON-1" identifierref="R-LESSON-1">
                  <title>Lesson 1</title>
                  <adlcp:masteryscore>70</adlcp:masteryscore>
                </item>
              </item>
              <item identifier="LESSON-2" identifierref="R-LESSON-2">
                <title>Lesson 2</title>
              </item>
            </organization>
          </organizations>
          <resources>
            <resource identifier="R-GLOSSARY" type="webcontent" adlcp:scormtype="asset" href="glossary.html">
              <file href="glossary.html"/>
            </resource>
            <resource identifier="R-LESSON-1" type="webcontent" adlcp:scormtype="sco" href="lesson1/start.htm">
              <file href="lesson1/start.htm"/>
              <dependency identifierref="R-SHARED"/>
            </resource>
            <resource identifier="R-LESSON-2" type="webcontent" adlcp:scormtype="sco" href="lesson2/start.htm">
              <file href="lesson2/start.htm"/>
            </resource>
            <resource identifier="R-SHARED" type="webcontent" adlcp:scormtype="asset">
              <file href="shared/api.js"/>
            </resource>
          </resources>
        </manifest>
        """;

    public const string AssetsOnly = """
        <?xml version="1.0" encoding="UTF-8"?>
        <manifest identifier="ASSETS" xmlns="http://www.imsproject.org/xsd/imscp_rootv1p1p2" xmlns:adlcp="http://www.adlnet.org/xsd/adlcp_rootv1p2">
          <organizations default="O">
            <organization identifier="O">
              <title>Handouts</title>
              <item identifier="I1" identifierref="R1"><title>Handout</title></item>
            </organization>
          </organizations>
          <resources>
            <resource identifier="R1" type="webcontent" adlcp:scormtype="asset" href="handout.html">
              <file href="handout.html"/>
            </resource>
          </resources>
        </manifest>
        """;

    public const string XmlBase = """
        <?xml version="1.0" encoding="UTF-8"?>
        <manifest identifier="BASE" xml:base="course/" xmlns="http://www.imsproject.org/xsd/imscp_rootv1p1p2" xmlns:adlcp="http://www.adlnet.org/xsd/adlcp_rootv1p2">
          <organizations default="O">
            <organization identifier="O">
              <title>Based</title>
              <item identifier="I1" identifierref="R1" parameters="?mode=lms"><title>Start</title></item>
            </organization>
          </organizations>
          <resources xml:base="content/">
            <resource identifier="R1" type="webcontent" adlcp:scormtype="sco" xml:base="sco1/" href="launch.html?lang=en">
              <file href="launch.html"/>
            </resource>
          </resources>
        </manifest>
        """;

    public const string MasteryScore = """
        <?xml version="1.0" encoding="UTF-8"?>
        <manifest identifier="MASTERY" xmlns="http://www.imsproject.org/xsd/imscp_rootv1p1p2" xmlns:adlcp="http://www.adlnet.org/xsd/adlcp_rootv1p2">
          <metadata><schema>ADL SCORM</schema><schemaversion>1.2</schemaversion></metadata>
          <organizations default="O">
            <organization identifier="O">
              <title>Exam</title>
              <item identifier="EXAM" identifierref="R1">
                <title>Final exam</title>
                <adlcp:prerequisites type="aicc_script">INTRO</adlcp:prerequisites>
                <adlcp:maxtimeallowed>00:30:00</adlcp:maxtimeallowed>
                <adlcp:timelimitaction>exit,message</adlcp:timelimitaction>
                <adlcp:datafromlms>level=2</adlcp:datafromlms>
                <adlcp:masteryscore>80</adlcp:masteryscore>
              </item>
            </organization>
          </organizations>
          <resources>
            <resource identifier="R1" type="webcontent" adlcp:scormtype="sco" href="exam.html"><file href="exam.html"/></resource>
          </resources>
        </manifest>
        """;

    public const string NoDefaultOrganization = """
        <?xml version="1.0" encoding="UTF-8"?>
        <manifest identifier="NODEFAULT" xmlns="http://www.imsproject.org/xsd/imscp_rootv1p1p2" xmlns:adlcp="http://www.adlnet.org/xsd/adlcp_rootv1p2">
          <organizations>
            <organization identifier="FIRST">
              <title>First organization</title>
              <item identifier="A" identifierref="RA"><title>A</title></item>
            </organization>
            <organization identifier="SECOND">
              <title>Second organization</title>
              <item identifier="B" identifierref="RB"><title>B</title></item>
            </organization>
          </organizations>
          <resources>
            <resource identifier="RA" type="webcontent" adlcp:scormtype="sco" href="a.html"><file href="a.html"/></resource>
            <resource identifier="RB" type="webcontent" adlcp:scormtype="sco" href="b.html"><file href="b.html"/></resource>
          </resources>
        </manifest>
        """;

    /// <summary>
    /// A SCORM 2004 3rd Edition single-SCO package without sequencing, written from the CAM: <c>adlcp:scormType</c>, the
    /// 3rd Edition <c>adlcp:completionThreshold</c> as text, <c>adlcp:dataFromLMS</c>, <c>adlcp:timeLimitAction</c> and
    /// <c>xml:base</c> on the resources and the resource.
    /// </summary>
    public const string Scorm2004Third = """
        <?xml version="1.0" encoding="UTF-8"?>
        <manifest identifier="com.example.s2004.third" version="1"
                  xmlns="http://www.imsglobal.org/xsd/imscp_v1p1"
                  xmlns:adlcp="http://www.adlnet.org/xsd/adlcp_v1p3"
                  xmlns:adlseq="http://www.adlnet.org/xsd/adlseq_v1p3"
                  xmlns:adlnav="http://www.adlnet.org/xsd/adlnav_v1p3"
                  xmlns:imsss="http://www.imsglobal.org/xsd/imsss"
                  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                  xsi:schemaLocation="http://www.imsglobal.org/xsd/imscp_v1p1 imscp_v1p1.xsd http://www.adlnet.org/xsd/adlcp_v1p3 adlcp_v1p3.xsd http://www.adlnet.org/xsd/adlseq_v1p3 adlseq_v1p3.xsd http://www.adlnet.org/xsd/adlnav_v1p3 adlnav_v1p3.xsd http://www.imsglobal.org/xsd/imsss imsss_v1p0.xsd">
          <metadata>
            <schema>ADL SCORM</schema>
            <schemaversion>2004 3rd Edition</schemaversion>
          </metadata>
          <organizations default="ORG">
            <organization identifier="ORG">
              <title>Hazard Communication</title>
              <item identifier="ITEM-HAZCOM" identifierref="RES-HAZCOM">
                <title>Hazard Communication Lesson</title>
                <adlcp:timeLimitAction>continue,message</adlcp:timeLimitAction>
                <adlcp:dataFromLMS>chapter=1</adlcp:dataFromLMS>
                <adlcp:completionThreshold>0.8</adlcp:completionThreshold>
              </item>
            </organization>
          </organizations>
          <resources xml:base="content/">
            <resource identifier="RES-HAZCOM" type="webcontent" adlcp:scormType="sco" xml:base="hazcom/" href="index.html?mode=lms">
              <file href="index.html"/>
            </resource>
          </resources>
        </manifest>
        """;

    /// <summary>
    /// A SCORM 2004 4th Edition package with sequencing: an organization and item <c>imsss:sequencing</c>, a primary
    /// objective satisfied by measure, a second objective, limit conditions, the 4th Edition
    /// <c>adlcp:completionThreshold</c> attributes, a second SCO and an asset.
    /// </summary>
    public const string Scorm2004Fourth = """
        <?xml version="1.0" encoding="UTF-8"?>
        <manifest identifier="com.example.s2004.fourth" version="1"
                  xmlns="http://www.imsglobal.org/xsd/imscp_v1p1"
                  xmlns:adlcp="http://www.adlnet.org/xsd/adlcp_v1p3"
                  xmlns:adlseq="http://www.adlnet.org/xsd/adlseq_v1p3"
                  xmlns:adlnav="http://www.adlnet.org/xsd/adlnav_v1p3"
                  xmlns:imsss="http://www.imsglobal.org/xsd/imsss">
          <metadata>
            <schema>ADL SCORM</schema>
            <schemaversion>2004 4th Edition</schemaversion>
          </metadata>
          <organizations default="ORG">
            <organization identifier="ORG" adlseq:objectivesGlobalToSystem="false">
              <title>Forklift Operation</title>
              <item identifier="ITEM-PRE" identifierref="RES-PRE">
                <title>Pre-operation Check</title>
                <adlcp:completionThreshold completedByMeasure="true" minProgressMeasure="0.75" progressWeight="1.0"/>
                <imsss:sequencing>
                  <imsss:limitConditions attemptAbsoluteDurationLimit="PT30M"/>
                  <imsss:objectives>
                    <imsss:primaryObjective objectiveID="PRIMARY-PRE" satisfiedByMeasure="true">
                      <imsss:minNormalizedMeasure>0.6</imsss:minNormalizedMeasure>
                    </imsss:primaryObjective>
                    <imsss:objective objectiveID="obj-mast-check"/>
                  </imsss:objectives>
                  <imsss:deliveryControls completionSetByContent="true" objectiveSetByContent="true"/>
                </imsss:sequencing>
              </item>
              <item identifier="ITEM-GLOSSARY" identifierref="RES-GLOSSARY">
                <title>Glossary</title>
              </item>
              <item identifier="ITEM-DRIVE" identifierref="RES-DRIVE">
                <title>Driving</title>
              </item>
              <imsss:sequencing>
                <imsss:controlMode choice="true" flow="true"/>
              </imsss:sequencing>
            </organization>
          </organizations>
          <resources>
            <resource identifier="RES-PRE" type="webcontent" adlcp:scormType="sco" href="pre/index.html">
              <file href="pre/index.html"/>
            </resource>
            <resource identifier="RES-GLOSSARY" type="webcontent" adlcp:scormType="asset" href="glossary.html">
              <file href="glossary.html"/>
            </resource>
            <resource identifier="RES-DRIVE" type="webcontent" adlcp:scormType="sco" href="drive/index.html">
              <file href="drive/index.html"/>
            </resource>
          </resources>
        </manifest>
        """;

    /// <summary>
    /// A SCORM 2004 package with no metadata: the SCORM 2004 namespaces alone say what it is.
    /// </summary>
    public const string Scorm2004NamespacesOnly = """
        <?xml version="1.0" encoding="UTF-8"?>
        <manifest identifier="S2004" xmlns="http://www.imsglobal.org/xsd/imscp_v1p1" xmlns:adlcp="http://www.adlnet.org/xsd/adlcp_v1p3">
          <organizations default="O">
            <organization identifier="O"><title>T</title><item identifier="I" identifierref="R"><title>I</title></item></organization>
          </organizations>
          <resources>
            <resource identifier="R" type="webcontent" adlcp:scormType="sco" href="index.html"><file href="index.html"/></resource>
          </resources>
        </manifest>
        """;

    public const string Doctype = """
        <?xml version="1.0"?>
        <!DOCTYPE manifest [ <!ENTITY xxe SYSTEM "file:///c:/windows/win.ini"> ]>
        <manifest identifier="X" xmlns="http://www.imsproject.org/xsd/imscp_rootv1p1p2"><organizations><organization identifier="O"><title>&xxe;</title></organization></organizations></manifest>
        """;

    public static string WithHref(string href)
        => SingleSco.Replace("href=\"index.html\">", $"href=\"{href}\">", StringComparison.Ordinal);
}
