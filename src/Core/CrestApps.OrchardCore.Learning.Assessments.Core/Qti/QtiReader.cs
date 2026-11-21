using System.Xml;
using System.Xml.Linq;
using CrestApps.OrchardCore.Archives;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Qti;

/// <summary>
/// The limits a QTI file must stay within.
/// </summary>
public sealed class QtiReadLimits
{
    /// <summary>
    /// Gets or sets the archive limits: entries, and the bytes read from the package (manifest, items and media).
    /// </summary>
    public PackageArchiveLimits Archive { get; set; } = new()
    {
        MaxEntries = 10_000,
        MaxExpandedBytes = 200L * 1024 * 1024,
    };

    /// <summary>
    /// Gets or sets the most items a package may list.
    /// </summary>
    public int MaxItems { get; set; } = 5_000;

    /// <summary>
    /// Gets or sets the largest media file an item may refer to.
    /// </summary>
    public long MaxMediaBytes { get; set; } = 10L * 1024 * 1024;

    /// <summary>
    /// Gets or sets the most characters an XML file may have.
    /// </summary>
    public long MaxXmlCharacters { get; set; } = 10L * 1024 * 1024;
}

/// <summary>
/// Reads a QTI 3.0 or QTI 2.1 content package (a zip archive with <c>imsmanifest.xml</c>), or a single assessment item
/// file, into questions. The archive passes the safe extraction rules of learning packages, and every XML file is read
/// with DTDs refused and no external entities.
/// </summary>
public static class QtiReader
{
    private const string ManifestPath = "imsmanifest.xml";

    /// <summary>
    /// Reads a QTI file.
    /// </summary>
    /// <param name="file">The file, readable and seekable; it stays open.</param>
    /// <param name="fileName">The name of the file, which tells a package (<c>.zip</c>) from an item (<c>.xml</c>).</param>
    /// <param name="limits">The limits, or <see langword="null"/> for the defaults.</param>
    /// <returns>The items, or why the file was refused.</returns>
    public static QtiReadResult Read(Stream file, string fileName, QtiReadLimits limits = null)
    {
        ArgumentNullException.ThrowIfNull(file);

        limits ??= new QtiReadLimits();

        return IsZip(file, fileName)
            ? ReadPackage(file, limits)
            : ReadSingleItem(file, fileName, limits);
    }

    /// <summary>
    /// Loads an XML file with DTDs refused, no resolver, and a size limit.
    /// </summary>
    /// <param name="content">The bytes.</param>
    /// <param name="maxCharacters">The most characters.</param>
    /// <returns>The document, or <see langword="null"/> when it is not well-formed, has a DTD or is too large.</returns>
    internal static XDocument LoadXml(byte[] content, long maxCharacters)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            MaxCharactersInDocument = maxCharacters,
            MaxCharactersFromEntities = 1024,
        };

        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var reader = XmlReader.Create(stream, settings);

            return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static bool IsZip(Stream file, string fileName)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);

        if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // No telling extension: a zip archive starts with "PK".
        var start = file.Position;
        var first = file.ReadByte();
        var second = file.ReadByte();
        file.Position = start;

        return first == 'P' && second == 'K';
    }

    private static QtiReadResult ReadSingleItem(Stream file, string fileName, QtiReadLimits limits)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;

        while ((read = file.Read(chunk, 0, chunk.Length)) > 0)
        {
            buffer.Write(chunk, 0, read);

            if (buffer.Length > limits.MaxXmlCharacters * 4)
            {
                return new QtiReadResult { Error = QtiErrors.NotQti };
            }
        }

        var path = PackagePath.Normalize(Path.GetFileName(fileName ?? string.Empty)) ?? "item.xml";

        // A single file has no package around it, so the media it refers to cannot be found.
        var item = QtiItemParser.Parse(buffer.ToArray(), path, null, limits);

        // A file that is not an assessment item at all is refused as a file, not as an item.
        return item.Refusal == QtiRefusals.NotAnItem
            ? new QtiReadResult { Error = QtiErrors.NotQti }
            : new QtiReadResult { Items = [item] };
    }

    private static QtiReadResult ReadPackage(Stream file, QtiReadLimits limits)
    {
        using var archive = PackageArchiveReader.Open(file, limits.Archive, out var refusal);

        if (archive is null)
        {
            return refusal.Error == PackageArchiveErrors.InvalidArchive
                ? new QtiReadResult { Error = QtiErrors.NotQti }
                : new QtiReadResult
                {
                    Error = QtiErrors.UnsafeArchive,
                    ErrorDetail = string.IsNullOrEmpty(refusal.Entry) ? refusal.Error : $"{refusal.Error}: {refusal.Entry}",
                };
        }

        if (!archive.TryRead(ManifestPath, out var manifestBytes, out var manifestError))
        {
            return manifestError is null
                ? new QtiReadResult { Error = QtiErrors.NoManifest }
                : new QtiReadResult { Error = QtiErrors.UnsafeArchive, ErrorDetail = $"{manifestError}: {ManifestPath}" };
        }

        var manifest = LoadXml(manifestBytes, limits.MaxXmlCharacters);

        if (manifest?.Root is null || manifest.Root.Name.LocalName != "manifest")
        {
            return new QtiReadResult { Error = QtiErrors.InvalidManifest };
        }

        var resources = ItemResources(manifest.Root).ToList();

        if (resources.Count == 0)
        {
            return new QtiReadResult { Error = QtiErrors.NoItems };
        }

        if (resources.Count > limits.MaxItems)
        {
            return new QtiReadResult { Error = QtiErrors.TooManyItems, ErrorDetail = limits.MaxItems.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        }

        var items = new List<QtiItem>();

        foreach (var (identifier, href) in resources)
        {
            var path = href is null || href.Contains("://", StringComparison.Ordinal) ? null : PackagePath.Normalize(href);

            if (path is null || !archive.Contains(path))
            {
                items.Add(new QtiItem
                {
                    Identifier = identifier,
                    Title = identifier,
                    Path = path ?? href,
                    Refusal = QtiRefusals.MissingFile,
                    RefusalDetail = path ?? href,
                });

                continue;
            }

            if (!archive.TryRead(path, out var content, out var error))
            {
                return new QtiReadResult { Error = QtiErrors.UnsafeArchive, ErrorDetail = $"{error}: {path}" };
            }

            var item = QtiItemParser.Parse(content, path, archive, limits);
            item.Identifier ??= identifier;
            item.Title ??= item.Identifier;
            items.Add(item);
        }

        return new QtiReadResult { Items = items };
    }

    // The item resources, in the order of the manifest, with their href under the xml:base of the manifest, the
    // resources element and the resource (IMS Content Packaging).
    private static IEnumerable<(string Identifier, string Href)> ItemResources(XElement root)
    {
        var rootBase = Base(root);

        foreach (var resourcesElement in root.Elements().Where(element => element.Name.LocalName == "resources"))
        {
            var resourcesBase = Combine(rootBase, Base(resourcesElement));

            foreach (var resource in resourcesElement.Elements().Where(element => element.Name.LocalName == "resource"))
            {
                var type = (string)resource.Attribute("type") ?? string.Empty;

                if (!IsItemType(type))
                {
                    continue;
                }

                var href = (string)resource.Attribute("href");

                if (string.IsNullOrWhiteSpace(href))
                {
                    // An item resource names its file in the first file element when it has no href.
                    href = (string)resource.Elements().FirstOrDefault(element => element.Name.LocalName == "file")?.Attribute("href");
                }

                yield return ((string)resource.Attribute("identifier"), href is null ? null : Combine(Combine(resourcesBase, Base(resource)), href));
            }
        }
    }

    private static bool IsItemType(string type)
        => type.StartsWith("imsqti_item_xmlv2p", StringComparison.OrdinalIgnoreCase)
        || type.StartsWith("imsqti_item_xmlv3p0", StringComparison.OrdinalIgnoreCase);

    private static string Base(XElement element)
        => (string)element.Attribute(XNamespace.Xml + "base");

    private static string Combine(string basePath, string path)
    {
        if (string.IsNullOrEmpty(basePath))
        {
            return path;
        }

        if (string.IsNullOrEmpty(path))
        {
            return basePath;
        }

        return basePath.EndsWith('/') ? basePath + path : basePath + "/" + path;
    }
}
