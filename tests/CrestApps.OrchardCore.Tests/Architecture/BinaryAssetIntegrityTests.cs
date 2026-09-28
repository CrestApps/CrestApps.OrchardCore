using System.Text;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// A binary asset that has been through a line-ending conversion is silently broken: the file is still served
/// with a 200 and the right content type, but every <c>0D 0A</c> byte pair inside it has collapsed to a bare
/// <c>0A</c>, so the signature no longer matches and no browser can decode it. It looks fine in the repo, fine in
/// the build, and shows as a broken-image icon only once someone loads the page — which is exactly how the
/// urgency-priority icons shipped corrupted.
/// <para>
/// This reads the first bytes of every shipped raster asset and checks the format's magic number. A file whose
/// magic is one <c>0D</c> short of correct has been normalized as text; <c>.gitattributes</c> marks these
/// extensions <c>binary</c> to prevent it, and this fails the build if one slips through anyway.
/// </para>
/// </summary>
public sealed class BinaryAssetIntegrityTests
{
    /// <summary>
    /// The magic-number prefix each raster format begins with. PNG is the one this guards most directly, because
    /// its signature deliberately contains a <c>0D 0A</c> pair specifically to detect this corruption.
    /// </summary>
    private static readonly (string Extension, byte[] Magic)[] _signatures =
    [
        (".png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        (".gif", "GIF8"u8.ToArray()),
        (".jpg", [0xFF, 0xD8, 0xFF]),
        (".jpeg", [0xFF, 0xD8, 0xFF]),
    ];

    [Fact]
    public void EveryShippedRasterAsset_HasAnIntactSignature()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src");
        var violations = new List<string>();

        // Act
        foreach (var (extension, magic) in _signatures)
        {
            foreach (var file in Directory.EnumerateFiles(sourceRoot, $"*{extension}", SearchOption.AllDirectories))
            {
                // Only the assets the running application serves to a browser: a corrupted one there is a broken
                // image on a live page. Build output and the documentation site's screenshots are generated or
                // maintained separately and are not part of the shipped app surface.
                if (!file.Contains($"{Path.DirectorySeparatorChar}wwwroot{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                var header = ReadHeader(file, magic.Length);

                if (!header.AsSpan().SequenceEqual(magic))
                {
                    var relativePath = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
                    var lfCorrupted = LooksLineEndingCorrupted(header, magic);

                    violations.Add(lfCorrupted
                        ? $"{relativePath}: signature is line-ending corrupted (a 0D byte was stripped). Restore the binary file and confirm .gitattributes marks {extension} as binary."
                        : $"{relativePath}: not a valid {extension.TrimStart('.').ToUpperInvariant()} (bad magic {Describe(header)}).");
                }
            }
        }

        // Assert
        Assert.True(violations.Count == 0, BuildMessage(violations));
    }

    private static bool LooksLineEndingCorrupted(byte[] header, byte[] expectedMagic)
    {
        // The tell is the exact damage: the expected 0D 0A pair now reads as a single 0A, so the header matches
        // the expected magic with every 0D removed.
        var expectedWithoutCr = expectedMagic.Where(b => b != 0x0D).ToArray();

        return header.Length >= expectedWithoutCr.Length &&
            header.AsSpan(0, expectedWithoutCr.Length).SequenceEqual(expectedWithoutCr);
    }

    private static byte[] ReadHeader(string file, int count)
    {
        using var stream = File.OpenRead(file);
        var buffer = new byte[count];
        var read = stream.Read(buffer, 0, count);

        return read == count ? buffer : buffer[..read];
    }

    private static string Describe(byte[] header)
        => string.Join(' ', header.Select(b => b.ToString("x2")));

    private static string BuildMessage(IReadOnlyList<string> lines)
    {
        var message = new StringBuilder().AppendLine();

        foreach (var line in lines.Order(StringComparer.Ordinal))
        {
            message.Append("    ").AppendLine(line);
        }

        return message.ToString();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test assembly location.");
    }
}
