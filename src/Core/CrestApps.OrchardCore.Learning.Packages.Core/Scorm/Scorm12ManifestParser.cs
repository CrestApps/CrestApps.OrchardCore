using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Scorm;

/// <summary>
/// Reads the manifest of a SCORM 1.2 package (ADL SCORM 1.2, Content Aggregation Model). Elements are matched by local
/// name, so the IMS content packaging namespace versions packages use in the field all read the same way. The default
/// organization is the one <c>organizations/@default</c> names, else the first. Its items are walked depth first in
/// document order; an item whose resource has <c>adlcp:scormtype="sco"</c> is a SCO, any other is an asset. A SCO's
/// file is its resource's <c>href</c> under the <c>xml:base</c> of the manifest, the resources element and the resource,
/// and it is opened with the resource's query and the item's <c>parameters</c>. A DTD is refused and never resolved.
/// </summary>
public static class Scorm12ManifestParser
{
    private static readonly XName _xmlBase = XNamespace.Xml + "base";

    /// <summary>
    /// Reads a manifest.
    /// </summary>
    /// <param name="manifest">The content of <c>imsmanifest.xml</c>.</param>
    /// <returns>The manifest, or why it was refused.</returns>
    public static Scorm12ManifestResult Parse(Stream manifest)
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
            return Scorm12ManifestResult.Fail(Scorm12ManifestErrors.InvalidXml);
        }

        var root = document.Root;

        if (root is null || root.Name.LocalName != "manifest")
        {
            return Scorm12ManifestResult.Fail(Scorm12ManifestErrors.InvalidXml);
        }

        var schemaVersion = Child(Child(root, "metadata"), "schemaversion")?.Value.Trim();

        if (IsScorm2004(root, schemaVersion))
        {
            return Scorm12ManifestResult.Fail(Scorm12ManifestErrors.Scorm2004);
        }

        var organizations = Child(root, "organizations");
        var all = Children(organizations, "organization").ToArray();
        var defaultId = Attribute(organizations, "default");
        var organization = all.FirstOrDefault(candidate => string.Equals(Attribute(candidate, "identifier"), defaultId, StringComparison.Ordinal)) ?? all.FirstOrDefault();

        if (organization is null || !Children(organization, "item").Any())
        {
            return Scorm12ManifestResult.Fail(Scorm12ManifestErrors.NoOrganization);
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
        var scos = new List<Scorm12Sco>();
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
                return Scorm12ManifestResult.Fail(error);
            }

            var masteryText = Child(item, "masteryscore")?.Value.Trim();
            decimal? mastery = null;

            if (!string.IsNullOrEmpty(masteryText))
            {
                if (!decimal.TryParse(masteryText, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) || value < 0 || value > 100)
                {
                    return Scorm12ManifestResult.Fail(Scorm12ManifestErrors.InvalidMasteryScore);
                }

                mastery = value;
            }

            scos.Add(new Scorm12Sco
            {
                ItemIdentifier = Attribute(item, "identifier"),
                Title = Child(item, "title")?.Value.Trim(),
                ResourceIdentifier = reference,
                Path = path,
                Query = query,
                MasteryScore = mastery,
                MaxTimeAllowed = Text(Child(item, "maxtimeallowed")),
                TimeLimitAction = Text(Child(item, "timelimitaction")),
                DataFromLms = Text(Child(item, "datafromlms")),
            });
        }

        if (scos.Count == 0)
        {
            return Scorm12ManifestResult.Fail(Scorm12ManifestErrors.NoSco);
        }

        return new Scorm12ManifestResult
        {
            Manifest = new Scorm12Manifest
            {
                Identifier = Attribute(root, "identifier"),
                Title = Text(Child(organization, "title")) ?? scos[0].Title,
                SchemaVersion = schemaVersion,
                OrganizationIdentifier = Attribute(organization, "identifier"),
                Scos = scos,
                AssetCount = assets,
            },
        };
    }

    private static string ResolveLaunch(string[] bases, string href, string parameters, out string path, out string query)
    {
        path = null;
        query = null;

        if (IsExternal(href) || bases.Any(IsExternal))
        {
            return Scorm12ManifestErrors.ExternalLaunch;
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
            return Scorm12ManifestErrors.UnsafeHref;
        }

        // Dot segments are resolved inside the package; climbing above its root is refused.
        var segments = new List<string>();

        foreach (var segment in decoded.Replace('\\', '/').Split('/'))
        {
            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    return Scorm12ManifestErrors.UnsafeHref;
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
            return Scorm12ManifestErrors.UnsafeHref;
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

    private static bool IsScorm2004(XElement root, string schemaVersion)
    {
        if (!string.IsNullOrEmpty(schemaVersion) &&
            (schemaVersion.Contains("2004", StringComparison.OrdinalIgnoreCase) || schemaVersion.Contains("CAM 1.3", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return root.Attributes().Any(attribute => attribute.IsNamespaceDeclaration &&
            (attribute.Value.Contains("adlcp_v1p3", StringComparison.OrdinalIgnoreCase) || attribute.Value.Contains("imsss", StringComparison.OrdinalIgnoreCase)));
    }

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
