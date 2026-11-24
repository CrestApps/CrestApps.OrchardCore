using System.IO.Compression;
using System.Text;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// Builds zip archives in memory, including hostile ones: any entry name, and the Unix mode bits of a symbolic link.
/// </summary>
internal static class PackageZip
{
    /// <summary>
    /// The Unix mode of a symbolic link (<c>S_IFLNK | 0777</c>), as the high word of the external attributes.
    /// </summary>
    public const int SymbolicLinkAttributes = unchecked((int)0xA1FF0000);

    public static MemoryStream Create(params (string Name, string Content)[] entries)
        => Create(entries.Select(entry => (entry.Name, Encoding.UTF8.GetBytes(entry.Content), 0)).ToArray());

    public static MemoryStream Create(params (string Name, byte[] Content, int ExternalAttributes)[] entries)
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content, attributes) in entries)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);

                if (attributes != 0)
                {
                    entry.ExternalAttributes = attributes;
                }

                using var writer = entry.Open();
                writer.Write(content);
            }
        }

        stream.Position = 0;

        return stream;
    }

    public static MemoryStream SingleScoPackage(string manifest = null, params (string Name, string Content)[] extra)
        => Create(
        [
            ("imsmanifest.xml", (manifest ?? ScormManifestSamples.SingleSco).TrimStart()),
            ("index.html", "<html><head><title>Lesson</title></head><body>Lesson</body></html>"),
            ("scripts/lesson.js", "var lesson = 1;"),
            .. extra,
        ]);
}
