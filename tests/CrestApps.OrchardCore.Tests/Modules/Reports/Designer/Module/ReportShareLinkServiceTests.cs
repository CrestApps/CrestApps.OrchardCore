using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Moq;
using OrchardCore.Modules;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module.ReportDesignerPrincipals;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

public sealed class ReportShareLinkServiceTests
{
    private static readonly DateTime _now = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreateAsync_StoresOnlyAHashOfTheToken()
    {
        // Arrange
        var catalog = Catalog<ReportShareLink>();
        var service = Service(catalog);

        // Act
        var (link, token) = await service.CreateAsync("r1", "Board pack", null, allowExport: true, requireSignIn: false, "admin");

        // Assert
        var stored = Assert.Single(await catalog.GetAllAsync(TestContext.Current.CancellationToken));
        Assert.True(token.Length >= 43);
        Assert.NotEqual(token, stored.TokenHash);
        Assert.DoesNotContain(token, stored.TokenHash, StringComparison.Ordinal);
        Assert.Equal(ReportShareLinkService.Hash(token), stored.TokenHash);
        Assert.Equal(token[..6], link.TokenHint);
        Assert.True(stored.AllowExport);
    }

    [Fact]
    public async Task FindActiveAsync_OpensTheLinkOnlyWithItsExactToken()
    {
        // Arrange
        var service = Service(Catalog<ReportShareLink>());
        var (link, token) = await service.CreateAsync("r1", null, null, false, false, "admin");

        // Act & Assert
        Assert.Equal(link.ItemId, (await service.FindActiveAsync(token))?.ItemId);
        Assert.Null(await service.FindActiveAsync(token + "x"));
        Assert.Null(await service.FindActiveAsync(token[..^1]));
        Assert.Null(await service.FindActiveAsync(string.Empty));
        Assert.Null(await service.FindActiveAsync(new string('a', 500)));
    }

    [Fact]
    public async Task FindActiveAsync_RefusesExpiredAndRevokedLinks()
    {
        // Arrange
        var service = Service(Catalog<ReportShareLink>());
        var (_, expired) = await service.CreateAsync("r1", null, _now.AddMinutes(-1), false, false, "admin");
        var (future, active) = await service.CreateAsync("r1", null, _now.AddDays(1), false, false, "admin");
        var (revokedLink, revoked) = await service.CreateAsync("r1", null, null, false, false, "admin");

        // Act
        Assert.True(await service.RevokeAsync("r1", revokedLink.ItemId));

        // Assert
        Assert.Null(await service.FindActiveAsync(expired));
        Assert.Equal(future.ItemId, (await service.FindActiveAsync(active))?.ItemId);
        Assert.Null(await service.FindActiveAsync(revoked));
    }

    [Fact]
    public async Task RevokeAsync_OnlyRevokesALinkOfTheGivenReport()
    {
        // Arrange
        var service = Service(Catalog<ReportShareLink>());
        var (link, token) = await service.CreateAsync("r1", null, null, false, false, "admin");

        // Act
        var revoked = await service.RevokeAsync("another-report", link.ItemId);

        // Assert
        Assert.False(revoked);
        Assert.NotNull(await service.FindActiveAsync(token));
    }

    [Fact]
    public async Task DeleteAllAsync_RemovesEveryLinkOfTheReport()
    {
        // Arrange
        var catalog = Catalog<ReportShareLink>();
        var service = Service(catalog);
        await service.CreateAsync("r1", null, null, false, false, "admin");
        await service.CreateAsync("r1", null, null, false, false, "admin");
        await service.CreateAsync("r2", null, null, false, false, "admin");

        // Act
        await service.DeleteAllAsync("r1");

        // Assert
        Assert.Equal("r2", Assert.Single(await catalog.GetAllAsync(TestContext.Current.CancellationToken)).ReportId);
    }

    private static ReportShareLinkService Service(CrestApps.Core.Services.ICatalog<ReportShareLink> catalog)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new ReportShareLinkService(catalog, clock.Object);
    }
}
