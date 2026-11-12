using CrestApps.OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.Tests.Core.FileStorage;

/// <summary>
/// SEC-A20 n: removing a tenant deleted the blobs under its base path by a raw prefix, so removing "TenantA" also deleted
/// "TenantAB/..." of another tenant in a shared container, and an empty base path matched the whole container.
/// </summary>
public sealed class TenantBlobPathsTests
{
    [Theory]
    [InlineData("TenantA", "TenantA/")]
    [InlineData("TenantA/", "TenantA/")]
    [InlineData("/TenantA/AIDocuments", "TenantA/AIDocuments/")]
    public void GetRemovalPrefix_EndsWithADelimiter(string basePath, string expected)
    {
        Assert.Equal(expected, TenantBlobPaths.GetRemovalPrefix(basePath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("  ")]
    public void GetRemovalPrefix_WithoutABasePath_RemovesNothing(string basePath)
    {
        Assert.Null(TenantBlobPaths.GetRemovalPrefix(basePath));
    }
}
