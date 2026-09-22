using CrestApps.OrchardCore.AI.FileSources;
using CrestApps.OrchardCore.AI.FileSources.Services;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Modules.AI.FileSources;

/// <summary>
/// Checks that a local file source can reach this tenant's own folder and nothing else.
/// </summary>
/// <remarks>
/// A tenant administrator configures the folder through the admin UI, so every one of these is a way that
/// screen could otherwise become a way to read another tenant's files, or any file the host process can
/// open. They are written against real directories because the rule is about what a path resolves to on
/// disk, and a fake file system would test the assertion rather than the behaviour.
/// </remarks>
public sealed class TenantFileSourceRootTests : IDisposable
{
    private readonly string _appData;
    private readonly TenantFileSourceRoot _root;

    public TenantFileSourceRootTests()
    {
        _appData = Path.Combine(Path.GetTempPath(), "cafs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_appData);

        _root = CreateRoot("TenantA");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_appData))
            {
                Directory.Delete(_appData, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test run over.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void GetRoot_IsTheTenantsOwnFolderUnderAppData()
    {
        var expected = Path.GetFullPath(Path.Combine(_appData, "Sites", "TenantA", FileSourceConstants.TenantFolderName));

        Assert.Equal(expected, _root.GetRoot());
    }

    [Fact]
    public void TwoTenants_DoNotShareAFolder()
    {
        Assert.NotEqual(_root.GetRoot(), CreateRoot("TenantB").GetRoot());
    }

    [Fact]
    public void EmptyPath_ResolvesToTheTenantFolderItself()
    {
        _root.EnsureRoot();

        Assert.True(_root.TryResolve(string.Empty, out var resolved, out var reason), reason);
        Assert.Equal(_root.GetRoot(), resolved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnAbsentPath_IsTheTenantFolderItself(string path)
    {
        // The editor leaves this blank by default, and blank has to mean the folder the reader was just
        // shown rather than an error they have to clear before they can save.
        _root.EnsureRoot();

        Assert.True(_root.TryResolve(path, out var resolved, out var reason), reason);
        Assert.Equal(_root.GetRoot(), resolved);
    }

    [Fact]
    public void ASubfolder_IsAccepted()
    {
        var inside = Path.Combine(_root.EnsureRoot(), "handbooks", "2026");
        Directory.CreateDirectory(inside);

        Assert.True(_root.TryResolve("handbooks/2026", out var resolved, out var reason), reason);
        Assert.Equal(Path.GetFullPath(inside), resolved);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../")]
    [InlineData("../../TenantB/file-sources")]
    [InlineData("handbooks/../../../TenantB/file-sources")]
    [InlineData("./../secrets")]
    public void APathThatClimbsOut_IsRefused(string path)
    {
        _root.EnsureRoot();

        // Normalized before it is compared. Checking the text a reader typed catches none of these: every
        // one of them reads as a path inside the folder right up until it is resolved.
        Assert.False(_root.TryResolve(path, out var resolved, out var reason), $"'{path}' resolved to '{resolved}'.");
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public void AnAbsolutePathOutsideTheRoot_IsRefused()
    {
        _root.EnsureRoot();

        var elsewhere = Path.Combine(_appData, "Sites", "TenantB", FileSourceConstants.TenantFolderName);
        Directory.CreateDirectory(elsewhere);

        Assert.False(_root.TryResolve(elsewhere, out _, out var reason));
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public void AnAbsolutePathInsideTheRoot_IsAccepted()
    {
        var inside = Path.Combine(_root.EnsureRoot(), "handbooks");
        Directory.CreateDirectory(inside);

        Assert.True(_root.TryResolve(inside, out var resolved, out var reason), reason);
        Assert.Equal(Path.GetFullPath(inside), resolved);
    }

    [Fact]
    public void ASiblingSharingANamePrefix_IsRefused()
    {
        var root = _root.EnsureRoot();

        // A plain string prefix comparison admits this one: ".../file-sources-other" starts with
        // ".../file-sources". Comparing as a path is what refuses it.
        var sibling = root + "-other";
        Directory.CreateDirectory(sibling);

        Assert.False(_root.TryResolve(sibling, out _, out var reason));
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public void ALinkLeavingTheRoot_IsRefused()
    {
        var root = _root.EnsureRoot();
        var outside = Path.Combine(_appData, "outside");
        Directory.CreateDirectory(outside);

        var link = Path.Combine(root, "escape");

        if (!TryCreateDirectoryLink(link, outside))
        {
            // Creating a symbolic link needs a privilege this account may not hold, and on Windows that is
            // the normal case. Skipping is honest; asserting on an unattempted link would not be.
            Assert.SkipWhen(true, "This account cannot create a directory link.");

            return;
        }

        // GetFullPath normalizes the text and stops; it does not follow a link. Walking to the real path is
        // the only thing that catches this.
        Assert.False(_root.TryResolve("escape", out var resolved, out var reason), $"'escape' resolved to '{resolved}'.");
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public void AFolderBeneathALinkThatLeavesTheRoot_IsRefused()
    {
        var root = _root.EnsureRoot();
        var outside = Path.Combine(_appData, "outside");
        Directory.CreateDirectory(Path.Combine(outside, "deep"));

        var link = Path.Combine(root, "escape");

        if (!TryCreateDirectoryLink(link, outside))
        {
            Assert.SkipWhen(true, "This account cannot create a directory link.");

            return;
        }

        // The link is not the final segment here. Checking only the last one would let this through.
        Assert.False(_root.TryResolve("escape/deep", out var resolved, out var reason), $"'escape/deep' resolved to '{resolved}'.");
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public void ALinkStayingInsideTheRoot_IsAccepted()
    {
        var root = _root.EnsureRoot();
        var target = Path.Combine(root, "real");
        Directory.CreateDirectory(target);

        var link = Path.Combine(root, "alias");

        if (!TryCreateDirectoryLink(link, target))
        {
            Assert.SkipWhen(true, "This account cannot create a directory link.");

            return;
        }

        Assert.True(_root.TryResolve("alias", out var resolved, out var reason), reason);
        Assert.Equal(Path.GetFullPath(target), resolved);
    }

    [Fact]
    public void AStoredValue_IsRelativeToTheTenantFolder()
    {
        var root = _root.EnsureRoot();
        var inside = Path.Combine(root, "handbooks", "2026");
        Directory.CreateDirectory(inside);

        Assert.True(_root.TryResolve(inside, out var resolved, out _));

        // Stored relative so it survives the tenant being renamed or the application being moved, and so
        // nothing persists a host-absolute path.
        Assert.Equal("handbooks/2026", _root.ToStoredValue(resolved));
        Assert.Equal(string.Empty, _root.ToStoredValue(root));
    }

    private TenantFileSourceRoot CreateRoot(string tenantName)
    {
        var shellSettings = new ShellSettings().AsUninitialized();
        shellSettings.Name = tenantName;

        var shellOptions = Options.Create(new ShellOptions
        {
            ShellsApplicationDataPath = _appData,
            ShellsContainerName = "Sites",
        });

        return new TenantFileSourceRoot(shellSettings, shellOptions);
    }

    /// <summary>
    /// Creates a directory link, falling back to a Windows junction when a symbolic link is not permitted.
    /// </summary>
    /// <param name="link">Where the link goes.</param>
    /// <param name="target">What it points at.</param>
    /// <returns><see langword="true"/> when a link now exists.</returns>
    /// <remarks>
    /// A symbolic link needs a privilege a developer or build account often does not hold on Windows, and
    /// skipping these would leave the one defence they exist to check unexercised. A junction needs no
    /// privilege, is resolved by the same API, and escapes a folder just as effectively.
    /// </remarks>
    private static bool TryCreateDirectoryLink(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);

            if (Directory.Exists(link))
            {
                return true;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
        }

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            process?.WaitForExit(15_000);

            return Directory.Exists(link);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
