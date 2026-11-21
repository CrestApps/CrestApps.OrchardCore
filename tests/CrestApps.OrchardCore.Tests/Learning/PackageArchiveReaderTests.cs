using CrestApps.OrchardCore.Archives;
using CrestApps.OrchardCore.Tests.Learning.Scorm;

namespace CrestApps.OrchardCore.Tests.Learning;

public sealed class PackageArchiveReaderTests
{
    [Fact]
    public void SafeArchive_ListsItsFilesWithNormalizedPaths_AndReadsThemIgnoringCase()
    {
        // Arrange
        using var zip = PackageZip.Create(("imsmanifest.xml", "<manifest/>"), ("items\\q1.xml", "<item/>"), ("images/", ""));

        // Act
        using var reader = PackageArchiveReader.Open(zip, new PackageArchiveLimits(), out var refusal);
        var read = reader.TryRead("Items/Q1.xml", out var content, out var error);

        // Assert
        Assert.Null(refusal);
        Assert.Equal(["imsmanifest.xml", "items/q1.xml"], reader.Files.Order(StringComparer.Ordinal));
        Assert.True(reader.Contains("ITEMS/q1.xml"));
        Assert.True(read, error);
        Assert.Equal("<item/>"u8.ToArray(), content);
        Assert.Equal(7, reader.ExpandedSize);
    }

    [Theory]
    [InlineData("../evil.xml")]
    [InlineData("/etc/evil.xml")]
    [InlineData("C:\\evil.xml")]
    [InlineData("items/../../evil.xml")]
    public void UnsafePath_IsRefusedWhenTheArchiveIsOpened(string name)
    {
        // Arrange
        using var zip = PackageZip.Create(("imsmanifest.xml", "<manifest/>"), (name, "pwned"));

        // Act
        var reader = PackageArchiveReader.Open(zip, new PackageArchiveLimits(), out var refusal);

        // Assert
        Assert.Null(reader);
        Assert.Equal(PackageArchiveErrors.UnsafePath, refusal.Error);
        Assert.Equal(name, refusal.Entry);
    }

    [Fact]
    public void SymbolicLinkOrTooManyEntries_AreRefused()
    {
        // Arrange
        using var link = PackageZip.Create(("a.xml", "a"u8.ToArray(), 0), ("b", "/etc/passwd"u8.ToArray(), PackageZip.SymbolicLinkAttributes));
        using var many = PackageZip.Create(Enumerable.Range(0, 4).Select(index => ($"f{index}.xml", "x")).ToArray());

        // Act
        PackageArchiveReader.Open(link, new PackageArchiveLimits(), out var linkRefusal);
        PackageArchiveReader.Open(many, new PackageArchiveLimits { MaxEntries = 3 }, out var manyRefusal);

        // Assert
        Assert.Equal(PackageArchiveErrors.SymbolicLink, linkRefusal.Error);
        Assert.Equal(PackageArchiveErrors.TooManyEntries, manyRefusal.Error);
    }

    [Fact]
    public void BytesRead_AreCountedAgainstTheLimit_AsTheyAreRead()
    {
        // Arrange
        // 64 KB of zeros compresses to a few hundred bytes: the limit is on what is read, not on the archive size.
        using var zip = PackageZip.Create(("small.txt", "ok"u8.ToArray(), 0), ("bomb.bin", new byte[64 * 1024], 0));
        using var reader = PackageArchiveReader.Open(zip, new PackageArchiveLimits { MaxExpandedBytes = 32 * 1024 }, out _);

        // Act
        var small = reader.TryRead("small.txt", out _, out var smallError);
        var bomb = reader.TryRead("bomb.bin", out var bombContent, out var bombError);
        var missing = reader.TryRead("nothing.txt", out _, out var missingError);

        // Assert
        Assert.True(small, smallError);
        Assert.False(bomb);
        Assert.Null(bombContent);
        Assert.Equal(PackageArchiveErrors.TooLarge, bombError);
        Assert.False(missing);
        Assert.Null(missingError);
    }

    [Fact]
    public void NotAZip_IsRefused()
    {
        // Arrange
        using var stream = new MemoryStream("not a zip"u8.ToArray());

        // Act
        var reader = PackageArchiveReader.Open(stream, new PackageArchiveLimits(), out var refusal);

        // Assert
        Assert.Null(reader);
        Assert.Equal(PackageArchiveErrors.InvalidArchive, refusal.Error);
    }
}
