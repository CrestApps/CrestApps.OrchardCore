using System.Security.Claims;
using System.Text;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.FileStorage.ProtectedFiles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.FileStorage;
using OrchardCore.FileStorage.FileSystem;

namespace CrestApps.OrchardCore.Tests.Core.FileStorage;

/// <summary>
/// Protected files: a signed, time-limited link opens a file only when the area has policies and every one of them
/// allows the person; an altered or expired link, or an area without policies, opens nothing.
/// </summary>
public sealed class ProtectedFileLinksTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"protected-{Guid.NewGuid():N}");

    [Fact]
    public async Task OpenAsync_OpensTheFile_WhenEveryPolicyAllowsThePerson()
    {
        // Arrange
        var store = Store();
        var file = await SaveAsync(store, "evidence.pdf", "certificate copy");
        var links = Links(store, new Policy(access => access.User.Identity?.Name == "ana" && access.ResourceId == "record-1"), new Policy(_ => true));
        var token = links.CreateToken(file.FileId, "record-1", TimeSpan.FromMinutes(5));

        // Act
        var opened = await links.OpenAsync(token, User("ana"), TestContext.Current.CancellationToken);
        var refused = await links.OpenAsync(token, User("ben"), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(opened);
        Assert.Equal("evidence.pdf", opened.Value.File.FileName);

        using (var reader = new StreamReader(opened.Value.Content))
        {
            Assert.Equal("certificate copy", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        }

        Assert.Null(refused);
    }

    [Fact]
    public async Task OpenAsync_OpensNothing_ForAnAlteredOrExpiredToken_OrAnAreaWithoutPolicies()
    {
        // Arrange
        var store = Store();
        var file = await SaveAsync(store, "notes.txt", "private");
        var links = Links(store, new Policy(_ => true));
        var withoutPolicies = Links(store);
        var token = links.CreateToken(file.FileId, null, TimeSpan.FromMinutes(5));
        var expired = links.CreateToken(file.FileId, null, TimeSpan.FromSeconds(-1));

        // Act
        var altered = await links.OpenAsync(token[..^2] + "AA", User("ana"), TestContext.Current.CancellationToken);
        var late = await links.OpenAsync(expired, User("ana"), TestContext.Current.CancellationToken);
        var noPolicy = await withoutPolicies.OpenAsync(withoutPolicies.CreateToken(file.FileId, null, TimeSpan.FromMinutes(5)), User("ana"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(altered);
        Assert.Null(late);
        Assert.Null(noPolicy);
    }

    [Fact]
    public async Task Store_RefusesIdsThatCouldLeaveTheFolder_AndDeletesAFile()
    {
        // Arrange
        var store = Store();
        var file = await SaveAsync(store, "../../escape.txt", "x");

        // Act
        var climbing = await store.FindAsync("../" + file.FileId, TestContext.Current.CancellationToken);
        var deleted = await store.DeleteAsync(file.FileId, TestContext.Current.CancellationToken);
        var after = await store.FindAsync(file.FileId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("escape.txt", file.FileName);
        Assert.Null(climbing);
        Assert.True(deleted);
        Assert.Null(after);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private ProtectedFileStore<TestArea> Store()
        => new(new FileSystemStore(_folder, NullLogger<FileSystemStore>.Instance), new FileCreationService([]));

    private static ProtectedFileLinks<TestArea> Links(ProtectedFileStore<TestArea> store, params IProtectedFileAccessPolicy<TestArea>[] policies)
        => new(new EphemeralDataProtectionProvider(), Options.Create(new ProtectedFileAreaOptions<TestArea> { Folder = "Test" }), store, policies);

    private static Task<ProtectedFile> SaveAsync(ProtectedFileStore<TestArea> store, string name, string content)
        => store.SaveAsync(name, new MemoryStream(Encoding.UTF8.GetBytes(content)), TestContext.Current.CancellationToken);

    private static ClaimsPrincipal User(string name)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], "test"));

    private sealed class TestArea
    {
    }

    private sealed class Policy : IProtectedFileAccessPolicy<TestArea>
    {
        private readonly Func<ProtectedFileAccess, bool> _allow;

        public Policy(Func<ProtectedFileAccess, bool> allow)
        {
            _allow = allow;
        }

        public Task<bool> CanReadAsync(ProtectedFileAccess access, CancellationToken cancellationToken = default)
            => Task.FromResult(_allow(access));
    }
}
