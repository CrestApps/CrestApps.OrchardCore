using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using CrestApps.OrchardCore.Archives;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Scorm;

/// <summary>
/// Reads the manifest of a SCORM 1.2 or SCORM 2004 package (the Content Aggregation Model of ADL SCORM 1.2 and of SCORM
/// 2004 3rd and 4th Edition). The standard comes from the metadata's <c>schemaversion</c>, else from the SCORM 2004
/// namespaces. Elements are matched by local name, so the IMS content packaging namespace versions packages use in the
/// field all read the same way. The default organization is the one <c>organizations/@default</c> names, else the
/// first. Its items are walked depth first in document order; an item whose resource has <c>adlcp:scormtype="sco"</c>
/// (<c>adlcp:scormType</c> in SCORM 2004) is a SCO, any other is an asset. A SCO's file is its resource's <c>href</c>
/// under the <c>xml:base</c> of the manifest, the resources element and the resource, and it is opened with the
/// resource's query and the item's <c>parameters</c>. SCORM 2004 sequencing is recorded and not applied. A DTD is
/// refused and never resolved.
/// </summary>
public static class ScormManifestParser
{
    private static readonly XName _xmlBase = XNamespace.Xml + "base";

    /// <summary>
    /// Reads a manifest.
    /// </summary>
    /// <param name="manifest">The content of <c>imsmanifest.xml</c>.</param>
    /// <returns>The manifest, or why it was refused.</returns>
    public static ScormManifestResult Parse(Stream manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        XDocument document;

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
            };

            using var reader = XmlReader.Create(manifest, settings);
            document = XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException)
        {
            return ScormManifestResult.Fail(ScormManifestErrors.InvalidXml);
        }

        var root = document.Root;

        if (root is null || root.Name.LocalName != "manifest")
        {
            return ScormManifestResult.Fail(ScormManifestErrors.InvalidXml);
        }

        var schemaVersion = Child(Child(root, "metadata"), "schemaversion")?.Value.Trim();

        var standard = DetectStandard(root, schemaVersion);

        var is2004 = PackagesConstants.Standards.IsScorm2004(standard);
        var sequencingCollection = is2004 ? Child(root, "sequencingCollection") : null;

        var organizations = Child(root, "organizations");
        var all = Children(organizations, "organization").ToArray();
        var defaultId = Attribute(organizations, "default");
        var organization = all.FirstOrDefault(candidate => string.Equals(Attribute(candidate, "identifier"), defaultId, StringComparison.Ordinal)) ?? all.FirstOrDefault();

        if (organization is null || !Children(organization, "item").Any())
        {
            return ScormManifestResult.Fail(ScormManifestErrors.NoOrganization);
        }

        var resourcesElement = Child(root, "resources");
        var resources = new Dictionary<string, XElement>(StringComparer.Ordinal);

        foreach (var resource in Children(resourcesElement, "resource"))
        {
            var identifier = Attribute(resource, "identifier");

            if (!string.IsNullOrEmpty(identifier))
            {
                resources.TryAdd(identifier, resource);
            }
        }

        var bases = new[] { (string)root.Attribute(_xmlBase), (string)resourcesElement?.Attribute(_xmlBase) };
        var scos = new List<ScormSco>();
        var assets = 0;

        foreach (var item in Descendants(organization))
        {
            var reference = Attribute(item, "identifierref");

            if (string.IsNullOrEmpty(reference) || !resources.TryGetValue(reference, out var resource))
            {
                continue;
            }

            if (!string.Equals(ScormType(resource), "sco", StringComparison.OrdinalIgnoreCase))
            {
                assets++;

                continue;
            }

            var href = Attribute(resource, "href");

            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var error = ResolveLaunch([.. bases, (string)resource.Attribute(_xmlBase)], href.Trim(), Attribute(item, "parameters"), out var path, out var query);

            if (error is not null)
            {
                return ScormManifestResult.Fail(error);
            }

            var sco = new ScormSco
            {
                ItemIdentifier = Attribute(item, "identifier"),
                Title = Child(item, "title")?.Value.Trim(),
                ResourceIdentifier = reference,
                Path = path,
                Query = query,
            };

            error = is2004 ? Read2004Item(item, sequencingCollection, sco) : Read12Item(item, sco);

            if (error is not null)
            {
                return ScormManifestResult.Fail(error);
            }

            scos.Add(sco);
        }

        // A manifest that says nothing of SCORM is an IMS content package: its items are web pages played without the
        // run-time API, and the first one is launched.
        if (scos.Count == 0 && !DeclaresScorm(root))
        {
            var error = ReadContentPackageItems(organization, resources, bases, scos);

            if (error is not null)
            {
                return ScormManifestResult.Fail(error);
            }

            standard = PackagesConstants.Standards.ImsContentPackage;
            assets = 0;
        }

        if (scos.Count == 0)
        {
            return ScormManifestResult.Fail(ScormManifestErrors.NoSco);
        }

        return new ScormManifestResult
        {
            Manifest = new ScormManifest
            {
                Standard = standard,
                Identifier = Attribute(root, "identifier"),
                Title = Text(Child(organization, "title")) ?? scos[0].Title,
                SchemaVersion = schemaVersion,
                OrganizationIdentifier = Attribute(organization, "identifier"),
                Scos = scos,
                AssetCount = assets,

                // Sequencing on the organization or an item, or a collection of shared sequencing definitions.
                HasSequencing = is2004 && (sequencingCollection is not null || organization.Descendants().Any(element => element.Name.LocalName == "sequencing")),
            },
        };
    }

    // SCORM 1.2 item values: adlcp:masteryscore (0 to 100), maxtimeallowed, timelimitaction and datafromlms.
    private static string Read12Item(XElement item, ScormSco sco)
    {
        var masteryText = Child(item, "masteryscore")?.Value.Trim();

        if (!string.IsNullOrEmpty(masteryText))
        {
            if (!TryParseNumber(masteryText, out var value) || value < 0 || value > 100)
            {
                return ScormManifestErrors.InvalidMasteryScore;
            }

            sco.MasteryScore = value;
        }

        sco.MaxTimeAllowed = Text(Child(item, "maxtimeallowed"));
        sco.TimeLimitAction = Text(Child(item, "timelimitaction"));
        sco.DataFromLms = Text(Child(item, "datafromlms"));

        return null;
    }

    // SCORM 2004 item values (CAM 4th Edition, section 3.4): adlcp:completionThreshold, adlcp:timeLimitAction,
    // adlcp:dataFromLMS, and from the item's imsss:sequencing (or the shared definition it names with IDRef) the
    // objectives, the primary objective's minimum normalized measure and the attempt duration limit.
    private static string Read2004Item(XElement item, XElement sequencingCollection, ScormSco sco)
    {
        sco.TimeLimitAction = Text(Child(item, "timeLimitAction"));
        sco.DataFromLms = Text(Child(item, "dataFromLMS"));

        var threshold = Child(item, "completionThreshold");

        if (threshold is not null)
        {
            var completedByMeasure = Attribute(threshold, "completedByMeasure");
            var minProgressMeasure = Attribute(threshold, "minProgressMeasure");

            // The 4th Edition writes the threshold as attributes and applies it only when the item is completed by
            // measure, with a minimum of 1.0 by default; the 3rd Edition writes it as the element's text.
            if (threshold.Attributes().Any(attribute => !attribute.IsNamespaceDeclaration && attribute.Name.Namespace == XNamespace.None))
            {
                if (IsTrue(completedByMeasure))
                {
                    if (!TryParseUnit(minProgressMeasure ?? "1.0", 0, out var value))
                    {
                        return ScormManifestErrors.InvalidCompletionThreshold;
                    }

                    sco.CompletionThreshold = value;
                }
            }
            else if (Text(threshold) is { } text)
            {
                if (!TryParseUnit(text, 0, out var value))
                {
                    return ScormManifestErrors.InvalidCompletionThreshold;
                }

                sco.CompletionThreshold = value;
            }
        }

        var sequencing = Child(item, "sequencing");

        if (sequencing is null)
        {
            return null;
        }

        sco.HasSequencing = true;

        var shared = Attribute(sequencing, "IDRef") is { Length: > 0 } reference
            ? Children(sequencingCollection, "sequencing").FirstOrDefault(candidate => string.Equals(Attribute(candidate, "ID"), reference, StringComparison.Ordinal))
            : null;

        // The item's own definition wins over the shared one it names.
        var objectives = Child(sequencing, "objectives") ?? Child(shared, "objectives");
        var limits = Child(sequencing, "limitConditions") ?? Child(shared, "limitConditions");

        sco.MaxTimeAllowed = Attribute(limits, "attemptAbsoluteDurationLimit") is { Length: > 0 } limit ? limit.Trim() : null;

        if (objectives is null)
        {
            return null;
        }

        var primary = Child(objectives, "primaryObjective");
        var ids = new List<string>();

        if (Attribute(primary, "objectiveID") is { Length: > 0 } primaryId)
        {
            ids.Add(primaryId.Trim());
        }

        foreach (var objective in Children(objectives, "objective"))
        {
            if (Attribute(objective, "objectiveID") is { Length: > 0 } id && !ids.Contains(id.Trim(), StringComparer.Ordinal))
            {
                ids.Add(id.Trim());
            }
        }

        sco.ObjectiveIds = ids;

        // The primary objective is satisfied by its measure: the minimum normalized measure (1.0 when it is not given)
        // is the scaled passing score of the SCO.
        if (primary is not null && IsTrue(Attribute(primary, "satisfiedByMeasure")))
        {
            if (!TryParseUnit(Text(Child(primary, "minNormalizedMeasure")) ?? "1.0", -1, out var passing))
            {
                return ScormManifestErrors.InvalidPassingScore;
            }

            sco.ScaledPassingScore = passing;
        }

        return null;
    }

    // SCORM marks its resources with adlcp:scormtype and names ADL in its namespaces or its metadata schema; an IMS
    // content package does neither.
    private static bool DeclaresScorm(XElement root)
    {
        var schema = Child(Child(root, "metadata"), "schema")?.Value;

        if (schema?.Contains("SCORM", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        foreach (var element in root.DescendantsAndSelf())
        {
            foreach (var attribute in element.Attributes())
            {
                if (string.Equals(attribute.Name.LocalName, "scormtype", StringComparison.OrdinalIgnoreCase) ||
                    (attribute.IsNamespaceDeclaration && attribute.Value.Contains("adlnet", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string ReadContentPackageItems(XElement organization, Dictionary<string, XElement> resources, string[] bases, List<ScormSco> items)
    {
        foreach (var item in Descendants(organization))
        {
            var reference = Attribute(item, "identifierref");

            if (string.IsNullOrEmpty(reference) || !resources.TryGetValue(reference, out var resource))
            {
                continue;
            }

            var href = Attribute(resource, "href");

            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var error = ResolveLaunch([.. bases, (string)resource.Attribute(_xmlBase)], href.Trim(), Attribute(item, "parameters"), out var path, out var query);

            if (error is not null)
            {
                return error;
            }

            items.Add(new ScormSco
            {
                ItemIdentifier = Attribute(item, "identifier"),
                Title = Child(item, "title")?.Value.Trim(),
                ResourceIdentifier = reference,
                Path = path,
                Query = query,
            });
        }

        return null;
    }

    // The standard is the metadata's schema version when it names one; otherwise the SCORM 2004 namespaces make the
    // package SCORM 2004, of the 4th Edition, whose limits are the larger, so content written for either edition keeps
    // its data. "CAM 1.3" and the 2nd Edition follow the 3rd Edition's rules.
    private static string DetectStandard(XElement root, string schemaVersion)
    {
        if (!string.IsNullOrEmpty(schemaVersion))
        {
            if (schemaVersion.Contains("2004", StringComparison.OrdinalIgnoreCase) && schemaVersion.Contains("4th", StringComparison.OrdinalIgnoreCase))
            {
                return PackagesConstants.Standards.Scorm2004FourthEdition;
            }

            if (schemaVersion.Contains("2004", StringComparison.OrdinalIgnoreCase) || schemaVersion.Contains("CAM 1.3", StringComparison.OrdinalIgnoreCase))
            {
                return PackagesConstants.Standards.Scorm2004ThirdEdition;
            }

            if (schemaVersion == "1.2")
            {
                return PackagesConstants.Standards.Scorm12;
            }
        }

        return IsScorm2004(root) ? PackagesConstants.Standards.Scorm2004FourthEdition : PackagesConstants.Standards.Scorm12;
    }

    private static bool IsTrue(string value)
        => value?.Trim() is "true" or "1";

    // A number from the minimum to 1, as a completion threshold (0 to 1) or a normalized measure (-1 to 1) is.
    private static bool TryParseUnit(string text, decimal minimum, out decimal value)
        => TryParseNumber(text, out value) && value >= minimum && value <= 1;

    private static bool TryParseNumber(string text, out decimal value)
        => decimal.TryParse(text?.Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

    private static string ResolveLaunch(string[] bases, string href, string parameters, out string path, out string query)
    {
        path = null;
        query = null;

        if (IsExternal(href) || bases.Any(IsExternal))
        {
            return ScormManifestErrors.ExternalLaunch;
        }

        var fragment = href.IndexOf('#', StringComparison.Ordinal);

        if (fragment >= 0)
        {
            href = href[..fragment];
        }

        var questionMark = href.IndexOf('?', StringComparison.Ordinal);
        var hrefQuery = questionMark >= 0 ? href[(questionMark + 1)..] : null;
        var hrefPath = questionMark >= 0 ? href[..questionMark] : href;

        var combined = string.Concat(bases.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim().TrimEnd('/') + "/")) + hrefPath;

        // An absolute path stays absolute, so the path rule refuses it below.
        string decoded;

        try
        {
            decoded = Uri.UnescapeDataString(combined);
        }
        catch (UriFormatException)
        {
            return ScormManifestErrors.UnsafeHref;
        }

        // Dot segments are resolved inside the package; climbing above its root is refused.
        var segments = new List<string>();

        foreach (var segment in decoded.Replace('\\', '/').Split('/'))
        {
            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    return ScormManifestErrors.UnsafeHref;
                }

                segments.RemoveAt(segments.Count - 1);

                continue;
            }

            segments.Add(segment);
        }

        var resolved = string.Join('/', segments);

        path = resolved.StartsWith('/') ? null : PackagePath.Normalize(resolved);

        if (path is null)
        {
            return ScormManifestErrors.UnsafeHref;
        }

        var extra = parameters?.Trim().TrimStart('?', '&');
        var fragmentStart = extra?.IndexOf('#', StringComparison.Ordinal) ?? -1;

        if (fragmentStart >= 0)
        {
            extra = extra[..fragmentStart];
        }

        query = (string.IsNullOrEmpty(hrefQuery), string.IsNullOrEmpty(extra)) switch
        {
            (true, true) => null,
            (false, true) => hrefQuery,
            (true, false) => extra,
            _ => hrefQuery + "&" + extra,
        };

        return null;
    }

    private static bool IsExternal(string value)
        => !string.IsNullOrEmpty(value) && (value.Contains("://", StringComparison.Ordinal) || value.StartsWith("//", StringComparison.Ordinal));

    // The namespaces only SCORM 2004 manifests declare: adlcp_v1p3, adlseq_v1p3, adlnav_v1p3 and the IMS Simple
    // Sequencing namespace.
    private static bool IsScorm2004(XElement root)
        => root.Attributes().Any(attribute => attribute.IsNamespaceDeclaration &&
            (attribute.Value.Contains("adlcp_v1p3", StringComparison.OrdinalIgnoreCase) ||
             attribute.Value.Contains("adlseq_v1p3", StringComparison.OrdinalIgnoreCase) ||
             attribute.Value.Contains("adlnav_v1p3", StringComparison.OrdinalIgnoreCase) ||
             attribute.Value.Contains("imsss", StringComparison.OrdinalIgnoreCase)));

    private static string ScormType(XElement resource)
        => resource.Attributes().FirstOrDefault(attribute => string.Equals(attribute.Name.LocalName, "scormtype", StringComparison.OrdinalIgnoreCase))?.Value.Trim();

    private static IEnumerable<XElement> Descendants(XElement parent)
    {
        foreach (var item in Children(parent, "item"))
        {
            yield return item;

            foreach (var child in Descendants(item))
            {
                yield return child;
            }
        }
    }

    private static XElement Child(XElement parent, string localName)
        => parent?.Elements().FirstOrDefault(element => element.Name.LocalName == localName);

    private static IEnumerable<XElement> Children(XElement parent, string localName)
        => parent?.Elements().Where(element => element.Name.LocalName == localName) ?? [];

    private static string Attribute(XElement element, string localName)
        => element?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName && attribute.Name.Namespace == XNamespace.None)?.Value;

    private static string Text(XElement element)
    {
        var value = element?.Value.Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }
}
