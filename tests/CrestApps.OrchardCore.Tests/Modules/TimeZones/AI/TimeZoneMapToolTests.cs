using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.TimeZones.AI;
using CrestApps.OrchardCore.TimeZones.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.TimeZones.AI;

/// <summary>
/// The time zone map tool needs <c>ManageTimeZoneMaps</c> and lists the maps as the Time Zones admin list does.
/// </summary>
public sealed class TimeZoneMapToolTests
{
    private readonly List<TimeZoneMap> _stored =
    [
        new TimeZoneMap { ItemId = "hawaii", Name = "Hawaii", TimeZoneId = "Pacific/Honolulu" },
    ];

    [Fact]
    public async Task ListTimeZoneMaps_RefusesACallerWhoIsNotSignedIn()
    {
        // Arrange
        var services = Build(signedIn: false);

        // Act
        var result = await InvokeAsync(services, new());

        // Assert
        Assert.Equal(TimeZonesAIToolBase.NotAllowedMessage, result);
    }

    [Fact]
    public async Task ListTimeZoneMaps_RefusesACallerWithoutManageTimeZoneMaps()
    {
        // Arrange
        var services = Build(authorize: (permission, _) => permission.Name != "ManageTimeZoneMaps");

        // Act
        var result = await InvokeAsync(services, new());

        // Assert
        Assert.Equal(TimeZonesAIToolBase.NotAllowedMessage, result);
        Assert.Equal("ManageTimeZoneMaps", Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
    }

    [Fact]
    public async Task ListTimeZoneMaps_FiltersByNameOrTimeZone()
    {
        // Arrange
        _stored.Add(new TimeZoneMap { ItemId = "denver", Name = "Mountain Time (Denver)", TimeZoneId = "America/Denver" });
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(services, new() { ["name"] = "america/" }));

        // Assert
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());

        Assert.Equal("Mountain Time (Denver)", item.GetProperty("name").GetString());
        Assert.Equal(1, json.GetProperty("total").GetInt32());
    }

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        var catalog = new Mock<INamedCatalog<TimeZoneMap>>();
        catalog.Setup(item => item.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IReadOnlyCollection<TimeZoneMap>>(_stored.Select(map => map.Clone()).ToArray()));
        services.AddSingleton(catalog.Object);

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(IServiceProvider services, Dictionary<string, object> arguments)
        => AgentToolTestContext.InvokeAsync(new ListTimeZoneMapsTool(), services, arguments);
}
