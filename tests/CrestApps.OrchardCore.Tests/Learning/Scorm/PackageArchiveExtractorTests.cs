using CrestApps.OrchardCore.Learning.Packages.Core.Services;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

public sealed class PackageArchiveExtractorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "crestapps-package-tests-" + Guid.NewGuid().ToString("n"));
    private readonly string _target;

    public PackageArchiveExtractorTests()
    {
        _target = Path.Combine(_root, "extract");
        Directory.CreateDirectory(_target);
    }

    [Fact]
    public async Task SafeArchive_IsExtractedWithNormalizedPaths()
    {
        using var zip = PackageZip.Create(("index.html", "<p>hi</p>"), ("scripts\\app.js", "1"), ("css/", ""), ("css/site.css", "body{}"));

        var result = await PackageArchiveExtractor.ExtractAsync(zip, _target, new PackageArchiveLimits(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(["css/site.css", "index.html", "scripts/app.js"], result.Files.Order(StringComparer.Ordinal));
        Assert.True(File.Exists(Path.Combine(_target, "scripts", "app.js")));
        Assert.Equal(9 + 1 + 6, result.ExpandedSize);
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("lesson/../../evil.txt")]
    [InlineData("..\\evil.txt")]
    [InlineData("/etc/evil.txt")]
    [InlineData("\\evil.txt")]
    [InlineData("C:/evil.txt")]
    [InlineData("C:\\evil.txt")]
    [InlineData("\\\\server\\share\\evil.txt")]
    [InlineData("lesson/file.txt:stream")]
    [InlineData("lesson/CON.txt")]
    [InlineData("lesson/trailing.")]
    public async Task UnsafePath_IsRefusedAndNothingIsWrittenOutside(string name)
    {
        using var zip = PackageZip.Create(("index.html", "ok"), (name, "pwned"));

        var result = await PackageArchiveExtractor.ExtractAsync(zip, _target, new PackageArchiveLimits(), TestContext.Current.CancellationToken);

        Assert.Equal(PackageArchiveErrors.UnsafePath, result.Error);
        Assert.Equal(name, result.Entry);
        Assert.False(File.Exists(Path.Combine(_root, "evil.txt")));
        Assert.Empty(Directory.EnumerateFiles(_root, "evil.txt", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task SymbolicLink_IsRefused()
    {
        using var zip = PackageZip.Create(("index.html", "ok"u8.ToArray(), 0), ("link", "/etc/passwd"u8.ToArray(), PackageZip.SymbolicLinkAttributes));

        var result = await PackageArchiveExtractor.ExtractAsync(zip, _target, new PackageArchiveLimits(), TestContext.Current.CancellationToken);

        Assert.Equal(PackageArchiveErrors.SymbolicLink, result.Error);
        Assert.Equal("link", result.Entry);
    }

    [Fact]
    public async Task TooManyEntries_IsRefusedBeforeAnythingIsWritten()
    {
        using var zip = PackageZip.Create(Enumerable.Range(0, 6).Select(index => ($"file{index}.txt", "x")).ToArray());

        var result = await PackageArchiveExtractor.ExtractAsync(zip, _target, new PackageArchiveLimits { MaxEntries = 5 }, TestContext.Current.CancellationToken);

        Assert.Equal(PackageArchiveErrors.TooManyEntries, result.Error);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_target));
    }

    [Fact]
    public async Task TooLargeOnceExtracted_IsRefused_CountingTheBytesWritten()
    {
        // 64 KB of zeros compresses to a few hundred bytes: the limit is on what is written, not the archive size.
        using var zip = PackageZip.Create(("index.html", "ok"u8.ToArray(), 0), ("bomb.bin", new byte[64 * 1024], 0));

        var result = await PackageArchiveExtractor.ExtractAsync(zip, _target, new PackageArchiveLimits { MaxExpandedBytes = 32 * 1024 }, TestContext.Current.CancellationToken);

        Assert.Equal(PackageArchiveErrors.TooLarge, result.Error);
        Assert.Equal("bomb.bin", result.Entry);
        Assert.True(zip.Length < 32 * 1024);
    }

    [Fact]
    public async Task DuplicatePathIgnoringCase_IsRefused()
    {
        using var zip = PackageZip.Create(("Index.html", "a"), ("index.html", "b"));

        var result = await PackageArchiveExtractor.ExtractAsync(zip, _target, new PackageArchiveLimits(), TestContext.Current.CancellationToken);

        Assert.Equal(PackageArchiveErrors.DuplicateEntry, result.Error);
    }

    [Fact]
    public async Task NotAZip_IsRefused()
    {
        using var stream = new MemoryStream("not a zip at all"u8.ToArray());

        var result = await PackageArchiveExtractor.ExtractAsync(stream, _target, new PackageArchiveLimits(), TestContext.Current.CancellationToken);

        Assert.Equal(PackageArchiveErrors.InvalidArchive, result.Error);
    }

    [Fact]
    public async Task OnlyFolders_IsRefusedAsEmpty()
    {
        using var zip = PackageZip.Create(("lesson/", ""));

        var result = await PackageArchiveExtractor.ExtractAsync(zip, _target, new PackageArchiveLimits(), TestContext.Current.CancellationToken);

        Assert.Equal(PackageArchiveErrors.Empty, result.Error);
    }

    [Theory]
    [InlineData("index.html", "index.html")]
    [InlineData("a\\b\\c.js", "a/b/c.js")]
    [InlineData("./a/./b.js", "a/b.js")]
    [InlineData("a//b.js", "a/b.js")]
    [InlineData("my lesson/page 1.html", "my lesson/page 1.html")]
    [InlineData("../a.js", null)]
    [InlineData("a/../b.js", null)]
    [InlineData("/a.js", null)]
    [InlineData("c:a.js", null)]
    [InlineData("a/aux.js", null)]
    [InlineData("a\u0001.js", null)]
    [InlineData("", null)]
    [InlineData(".", null)]
    public void PackagePath_Normalizes(string path, string expected)
    {
        Assert.Equal(expected, PackagePath.Normalize(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
