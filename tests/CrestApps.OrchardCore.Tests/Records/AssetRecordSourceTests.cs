using CrestApps.OrchardCore.Assets;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Assets;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Records;

/// <summary>
/// Assets are a record kind: a user who may see assets finds them by name, number or serial number, read in the
/// database, and any record that links to an asset shows its name, number, type and serial number.
/// </summary>
public sealed class AssetRecordSourceTests
{
    [Fact]
    public async Task ASearch_FindsAssetsByNameNumberOrSerialNumber_ForAUserWhoMaySeeAssets()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var type = await AssetsTests.AddTypeAsync(harness);
        var pumpId = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<AssetManager>();
            var boiler = await manager.NewAsync();

            boiler.Name = "Boiler";
            boiler.AssetTypeId = type.ItemId;
            await manager.CreateAsync(boiler);

            var pump = await manager.NewAsync();

            pump.Name = "Circulating pump";
            pump.AssetTypeId = type.ItemId;
            pump.SerialNumber = "SN-77";
            await manager.CreateAsync(pump);

            var forklift = await manager.NewAsync();

            forklift.Name = "Forklift";
            forklift.AssetTypeId = type.ItemId;
            await manager.CreateAsync(forklift);

            return pump.ItemId;
        });

        var viewer = CalendarTestHarness.User("viewer", AssetsPermissions.ViewAssets.Name);
        var stranger = CalendarTestHarness.User("stranger");

        // Act
        var result = await harness.RunAsync(async services =>
        {
            var directory = services.GetRequiredService<IRecordDirectory>();

            return (
                ByName: await directory.SearchAsync(AssetsConstants.RecordKind, new RecordSearch { Text = "pump", User = viewer }, TestContext.Current.CancellationToken),
                BySerialNumber: await directory.SearchAsync(AssetsConstants.RecordKind, new RecordSearch { Text = "SN-77", User = viewer }, TestContext.Current.CancellationToken),
                ByNumber: await directory.SearchAsync(AssetsConstants.RecordKind, new RecordSearch { Text = "A-0000", User = viewer, Count = 2 }, TestContext.Current.CancellationToken),
                First: await directory.SearchAsync(AssetsConstants.RecordKind, new RecordSearch { User = viewer }, TestContext.Current.CancellationToken),
                Forbidden: await directory.SearchAsync(AssetsConstants.RecordKind, new RecordSearch { Text = "pump", User = stranger }, TestContext.Current.CancellationToken),
                Summary: await directory.GetSummaryAsync(new RecordRef(AssetsConstants.RecordKind, pumpId), TestContext.Current.CancellationToken),
                Missing: await directory.GetSummaryAsync(new RecordRef(AssetsConstants.RecordKind, "missing"), TestContext.Current.CancellationToken));
        }, commit: false);

        // Assert
        var found = Assert.Single(result.ByName);

        Assert.Equal(pumpId, found.Id);
        Assert.Equal("Circulating pump", found.Title);
        Assert.Equal("A-00002, Equipment, SN-77", found.Description);
        Assert.Equal($"/Admin/assets/view/{pumpId}", found.AdminUrl);
        Assert.Equal(pumpId, Assert.Single(result.BySerialNumber).Id);
        Assert.Equal(["Boiler", "Circulating pump"], result.ByNumber.Select(record => record.Title));
        Assert.Equal(["Boiler", "Circulating pump", "Forklift"], result.First.Select(record => record.Title));
        Assert.Empty(result.Forbidden);
        Assert.Equal("Circulating pump", result.Summary.Title);
        Assert.Null(result.Missing);
    }

    private static Task<CalendarTestHarness> CreateAsync()
    {
        var httpContextAccessor = new Mock<IHttpContextAccessor>();

        httpContextAccessor.SetupGet(accessor => accessor.HttpContext).Returns(new DefaultHttpContext());

        return AssetsTests.CreateAsync(services =>
        {
            services.AddSingleton<LinkGenerator>(new TemplateLinkGenerator((_, values) => $"/Admin/assets/view/{values["id"]}"));
            services.AddSingleton(httpContextAccessor.Object);
            services.AddRecordSource<AssetRecordSource>();
        });
    }
}
