using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.TimeZones.AI;
using CrestApps.OrchardCore.TimeZones.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.TimeZones.AI;

/// <summary>
/// The time zone map tools need <c>ManageTimeZoneMaps</c>, run the time zone map handler's checks through the manager,
/// keep a name unique, and see the maps changed earlier in the same chat message although the catalog reads a cached
/// document until the scope commits.
/// </summary>
public sealed class TimeZoneMapToolTests
{
    private static readonly string[] _validZones = ["America/Denver", "Pacific/Honolulu", "America/Chicago"];

    private readonly List<string> _actions = [];
    private readonly List<TimeZoneMap> _stored =
    [
        new TimeZoneMap { ItemId = "hawaii", Name = "Hawaii", TimeZoneId = "Pacific/Honolulu" },
    ];

    public static TheoryData<string> ToolNames => new()
    {
        ListTimeZoneMapsTool.TheName,
        SaveTimeZoneMapTool.TheName,
        DeleteTimeZoneMapTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Arrange
        var services = Build(signedIn: false);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        Assert.Equal(TimeZonesAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_actions);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutManageTimeZoneMaps(string toolName)
    {
        // Arrange
        var services = Build(authorize: (permission, _) => permission.Name != "ManageTimeZoneMaps");

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        Assert.Equal(TimeZonesAIToolBase.NotAllowedMessage, result);
        Assert.Equal("ManageTimeZoneMaps", Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task ListTimeZoneMaps_FiltersByNameOrTimeZone()
    {
        // Arrange
        _stored.Add(new TimeZoneMap { ItemId = "denver", Name = "Mountain Time (Denver)", TimeZoneId = "America/Denver" });
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ListTimeZoneMapsTool.TheName, services, new() { ["name"] = "america/" }));

        // Assert
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());

        Assert.Equal("Mountain Time (Denver)", item.GetProperty("name").GetString());
        Assert.Equal(1, json.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task SaveTimeZoneMap_CreatesANewName_AndUpdatesAnExistingOne()
    {
        // Arrange
        var services = Build();

        // Act
        var created = AgentToolTestContext.ReadJson(await InvokeAsync(SaveTimeZoneMapTool.TheName, services, new() { ["name"] = "Mountain Time (Denver)", ["timeZoneId"] = "America/Denver" }));
        var updated = AgentToolTestContext.ReadJson(await InvokeAsync(SaveTimeZoneMapTool.TheName, services, new() { ["name"] = "hawaii", ["timeZoneId"] = "America/Chicago" }));

        // Assert
        Assert.True(created.GetProperty("created").GetBoolean());
        Assert.False(updated.GetProperty("created").GetBoolean());
        Assert.Equal("Pacific/Honolulu", updated.GetProperty("previousTimeZoneId").GetString());
        Assert.Equal(new[] { "validate", "create:Mountain Time (Denver)", "validate", "update:Hawaii" }, _actions);
    }

    [Fact]
    public async Task SaveTimeZoneMap_RelaysTheHandlersChecks()
    {
        // Arrange
        var services = Build();

        // Act
        var result = await InvokeAsync(SaveTimeZoneMapTool.TheName, services, new() { ["name"] = "Nowhere", ["timeZoneId"] = "Mars/Olympus" });

        // Assert
        Assert.Equal("Nothing was saved: The selected time zone is invalid.", result);
        Assert.Equal(new[] { "validate" }, _actions);
    }

    [Fact]
    public async Task TheTools_SeeTheMapsChangedEarlierInTheSameScope()
    {
        // Arrange: the catalog keeps returning the stored maps, as the cached document does until the scope commits.
        var services = Build();

        // Act
        await InvokeAsync(SaveTimeZoneMapTool.TheName, services, new() { ["name"] = "Mountain Time (Denver)", ["timeZoneId"] = "America/Denver" });
        var sameZone = await InvokeAsync(SaveTimeZoneMapTool.TheName, services, new() { ["name"] = "Denver", ["timeZoneId"] = "America/Denver" });
        var listed = AgentToolTestContext.ReadJson(await InvokeAsync(ListTimeZoneMapsTool.TheName, services, new()));
        var deleted = AgentToolTestContext.ReadJson(await InvokeAsync(DeleteTimeZoneMapTool.TheName, services, new() { ["name"] = "Mountain Time (Denver)" }));
        var deletedAgain = await InvokeAsync(DeleteTimeZoneMapTool.TheName, services, new() { ["name"] = "Mountain Time (Denver)" });

        // Assert
        Assert.Contains("already has the friendly name 'Mountain Time (Denver)'", sameZone, StringComparison.Ordinal);
        Assert.Equal(2, listed.GetProperty("total").GetInt32());
        Assert.True(deleted.GetProperty("deleted").GetBoolean());
        Assert.StartsWith("Nothing was deleted: no time zone map is named", deletedAgain, StringComparison.Ordinal);
        Assert.Contains("delete:Mountain Time (Denver)", _actions);
    }

    [Fact]
    public async Task DeleteTimeZoneMap_SaysWhenTheMapDoesNotExist()
    {
        // Arrange
        var services = Build();

        // Act
        var result = await InvokeAsync(DeleteTimeZoneMapTool.TheName, services, new() { ["id"] = "missing" });

        // Assert
        Assert.StartsWith("Nothing was deleted: no time zone map has the id 'missing'.", result, StringComparison.Ordinal);
        Assert.Empty(_actions);
    }

    private static Dictionary<string, object> Arguments(string toolName)
        => toolName switch
        {
            SaveTimeZoneMapTool.TheName => new() { ["name"] = "Hawaii", ["timeZoneId"] = "Pacific/Honolulu" },
            DeleteTimeZoneMapTool.TheName => new() { ["name"] = "Hawaii" },
            _ => new(),
        };

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        var catalog = new Mock<INamedCatalog<TimeZoneMap>>();
        catalog.Setup(item => item.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IReadOnlyCollection<TimeZoneMap>>(_stored.Select(map => map.Clone()).ToArray()));
        services.AddSingleton(catalog.Object);

        var manager = new Mock<INamedCatalogManager<TimeZoneMap>>();
        manager.Setup(item => item.NewAsync(It.IsAny<string>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((string name, JsonNode _, CancellationToken _) => ValueTask.FromResult(new TimeZoneMap { ItemId = Guid.NewGuid().ToString("N"), Name = name }));
        manager.Setup(item => item.ValidateAsync(It.IsAny<TimeZoneMap>(), It.IsAny<CancellationToken>()))
            .Returns((TimeZoneMap map, CancellationToken _) =>
            {
                _actions.Add("validate");

                var result = new ValidationResultDetails();

                if (!_validZones.Contains(map.TimeZoneId))
                {
                    result.Fail(new ValidationResult("The selected time zone is invalid."));
                }

                return ValueTask.FromResult(result);
            });
        manager.Setup(item => item.CreateAsync(It.IsAny<TimeZoneMap>(), It.IsAny<CancellationToken>()))
            .Callback((TimeZoneMap map, CancellationToken _) => _actions.Add("create:" + map.Name))
            .Returns(ValueTask.CompletedTask);
        manager.Setup(item => item.UpdateAsync(It.IsAny<TimeZoneMap>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Callback((TimeZoneMap map, JsonNode _, CancellationToken _) => _actions.Add("update:" + map.Name))
            .Returns(ValueTask.CompletedTask);
        manager.Setup(item => item.DeleteAsync(It.IsAny<TimeZoneMap>(), It.IsAny<CancellationToken>()))
            .Returns((TimeZoneMap map, CancellationToken _) =>
            {
                _actions.Add("delete:" + map.Name);

                return ValueTask.FromResult(true);
            });
        services.AddSingleton(manager.Object);

        services.AddScoped<TimeZoneMapScopeChanges>();

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            ListTimeZoneMapsTool.TheName => new ListTimeZoneMapsTool(),
            SaveTimeZoneMapTool.TheName => new SaveTimeZoneMapTool(),
            _ => new DeleteTimeZoneMapTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }
}
