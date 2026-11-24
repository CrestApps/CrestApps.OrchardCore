using System.Text.Json;
using CrestApps.OrchardCore.Geo;
using CrestApps.OrchardCore.Geo.AI;
using CrestApps.OrchardCore.Geo.Core.Indexes;
using CrestApps.OrchardCore.Geo.Core.Services;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Geo.AI;

/// <summary>
/// The territory tools run the territory manager's checks on a real store, merge members by kind as the editor does,
/// move a deleted territory's children up, and find territories with the resolver; finding the territory of a street
/// address geocodes it, which needs <c>ManageGeoSettings</c> too, and never shows a provider key.
/// </summary>
public sealed class GeoToolTests
{
    private readonly Mock<IGeoService> _geoService = new();
    private readonly GeoSettings _settings = new();

    public static TheoryData<string, string> ToolPermissions => new()
    {
        { ListServiceTerritoriesTool.TheName, "ManageGeoTerritories" },
        { SaveServiceTerritoryTool.TheName, "ManageGeoTerritories" },
        { DeleteServiceTerritoryTool.TheName, "ManageGeoTerritories" },
        { FindServiceTerritoryTool.TheName, "ManageGeoTerritories" },
    };

    [Theory]
    [MemberData(nameof(ToolPermissions))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName, string permission)
    {
        // Arrange
        await using var harness = await CreateAsync(signedIn: false);

        // Act
        var result = await harness.RunAsync(services => InvokeAsync(toolName, services, new()), commit: false);

        // Assert
        Assert.NotNull(permission);
        Assert.Equal(GeoAIToolBase.NotAllowedMessage, result);
        _geoService.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(ToolPermissions))]
    public async Task Tool_RefusesACallerWithoutItsPermission(string toolName, string permission)
    {
        // Arrange
        await using var harness = await CreateAsync(authorize: (asked, _) => asked.Name != permission);

        // Act
        var result = await harness.RunAsync(services => InvokeAsync(toolName, services, new()), commit: false);

        // Assert
        Assert.Equal(GeoAIToolBase.NotAllowedMessage, result);
        Assert.Equal(permission, Assert.Single(harness.Services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        _geoService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SaveServiceTerritory_CreatesATerritory_ThatListAndFindReturn()
    {
        // Arrange
        await using var harness = await CreateAsync();

        // Act
        var saved = await harness.RunAsync(services => InvokeJsonAsync(SaveServiceTerritoryTool.TheName, services, new()
        {
            ["name"] = "North Austin",
            ["postalCodes"] = new[] { "78758", "787 59", "786*" },
            ["timeZoneId"] = "america/chicago",
            ["travelSpeedKmh"] = 50,
            ["members"] = new Dictionary<string, string[]> { ["Team"] = ["team-1"] },
        }));
        var listed = await harness.RunAsync(services => InvokeJsonAsync(ListServiceTerritoriesTool.TheName, services, new() { ["name"] = "austin" }), commit: false);
        var found = await harness.RunAsync(services => InvokeJsonAsync(FindServiceTerritoryTool.TheName, services, new() { ["postalCode"] = "78759" }), commit: false);
        var byPrefix = await harness.RunAsync(services => InvokeJsonAsync(FindServiceTerritoryTool.TheName, services, new() { ["postalCode"] = "78610" }), commit: false);

        // Assert
        var territory = saved.GetProperty("territory");
        var item = Assert.Single(listed.GetProperty("items").EnumerateArray());
        var member = Assert.Single(territory.GetProperty("members").EnumerateArray());

        Assert.True(saved.GetProperty("created").GetBoolean());
        Assert.Equal(new[] { "78758", "78759", "786*" }, territory.GetProperty("postalCodes").EnumerateArray().Select(code => code.GetString()));
        Assert.Equal("America/Chicago", territory.GetProperty("timeZoneId").GetString());
        Assert.Equal("Team One", member.GetProperty("name").GetString());
        Assert.Equal(territory.GetProperty("id").GetString(), item.GetProperty("id").GetString());
        Assert.Equal("PostalCode", found.GetProperty("matchedBy").GetString());
        Assert.Equal("North Austin", found.GetProperty("territory").GetProperty("name").GetString());
        Assert.True(byPrefix.GetProperty("found").GetBoolean());
    }

    [Fact]
    public async Task SaveServiceTerritory_KeepsTheMembersOfKindsNotGiven()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var id = await CreateTerritoryAsync(harness, "Austin", members: new Dictionary<string, string[]> { ["Team"] = ["team-1"], ["Worker"] = ["worker-1"] });

        // Act
        var renamed = await harness.RunAsync(services => InvokeJsonAsync(SaveServiceTerritoryTool.TheName, services, new() { ["id"] = id, ["name"] = "Greater Austin" }));
        var cleared = await harness.RunAsync(services => InvokeJsonAsync(SaveServiceTerritoryTool.TheName, services, new() { ["id"] = id, ["members"] = new Dictionary<string, string[]> { ["Team"] = [] } }));

        // Assert
        Assert.Equal(2, renamed.GetProperty("territory").GetProperty("members").GetArrayLength());
        Assert.Equal("Greater Austin", renamed.GetProperty("territory").GetProperty("name").GetString());
        Assert.Equal("worker-1", Assert.Single(cleared.GetProperty("territory").GetProperty("members").EnumerateArray()).GetProperty("id").GetString());
    }

    [Theory]
    [InlineData("members", "Nothing was saved: 'team-9' is not one of the Teams that can serve a territory.")]
    [InlineData("kind", "Nothing was saved: 'Robots' is not a member kind.")]
    [InlineData("timeZone", "Nothing was saved: 'Mars/Olympus' is not a time zone the time zone pickers offer.")]
    [InlineData("parent", "Nothing was saved: no service territory has the id 'nowhere' to be the parent.")]
    [InlineData("empty", "Nothing was saved: Draw the territory on the map, list its postal codes, or both.")]
    public async Task SaveServiceTerritory_RefusesWhatTheEditorWouldNotOffer(string problem, string expected)
    {
        // Arrange
        await using var harness = await CreateAsync();
        var arguments = new Dictionary<string, object> { ["name"] = "Austin", ["postalCodes"] = new[] { "78701" } };

        switch (problem)
        {
            case "members":
                arguments["members"] = new Dictionary<string, string[]> { ["Team"] = ["team-9"] };
                break;
            case "kind":
                arguments["members"] = new Dictionary<string, string[]> { ["Robots"] = ["r2"] };
                break;
            case "timeZone":
                arguments["timeZoneId"] = "Mars/Olympus";
                break;
            case "parent":
                arguments["parentId"] = "nowhere";
                break;
            default:
                arguments["postalCodes"] = Array.Empty<string>();
                break;
        }

        // Act
        var result = await harness.RunAsync(services => InvokeAsync(SaveServiceTerritoryTool.TheName, services, arguments));
        var stored = await harness.RunAsync(async services => (await services.GetRequiredService<ServiceTerritoryManager>().GetAllAsync()).Count(), commit: false);

        // Assert
        Assert.StartsWith(expected, result, StringComparison.Ordinal);
        Assert.Equal(0, stored);
    }

    [Fact]
    public async Task DeleteServiceTerritory_MovesItsChildrenUpToItsParent()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var texas = await CreateTerritoryAsync(harness, "Texas");
        var austin = await CreateTerritoryAsync(harness, "Austin", parentId: texas);
        var north = await CreateTerritoryAsync(harness, "North Austin", parentId: austin);

        // Act
        var deleted = await harness.RunAsync(services => InvokeJsonAsync(DeleteServiceTerritoryTool.TheName, services, new() { ["id"] = austin }));
        var missing = await harness.RunAsync(services => InvokeAsync(DeleteServiceTerritoryTool.TheName, services, new() { ["id"] = austin }));
        var child = await harness.RunAsync(services => services.GetRequiredService<ServiceTerritoryManager>().FindByIdAsync(north).AsTask(), commit: false);

        // Assert
        Assert.Equal(north, Assert.Single(deleted.GetProperty("movedChildren").EnumerateArray()).GetProperty("id").GetString());
        Assert.Equal(texas, child.ParentId);
        Assert.StartsWith("Nothing was deleted: no service territory has the id", missing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindServiceTerritory_NeedsAPointOrAPostalCode()
    {
        // Arrange
        await using var harness = await CreateAsync();

        // Act
        var nothing = await harness.RunAsync(services => InvokeAsync(FindServiceTerritoryTool.TheName, services, new()), commit: false);
        var half = await harness.RunAsync(services => InvokeAsync(FindServiceTerritoryTool.TheName, services, new() { ["latitude"] = 30.2 }), commit: false);

        // Assert
        Assert.Equal("Nothing was searched: give a street address (address or text), a latitude and longitude, or a postal code.", nothing);
        Assert.Equal("Nothing was searched: give both latitude and longitude, or neither.", half);
    }

    [Fact]
    public async Task FindServiceTerritory_GeocodesAStreetAddress_AndFindsItsTerritory()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await CreateTerritoryAsync(harness, "Austin");
        GeocodeQuery asked = null;

        _geoService.Setup(service => service.GeocodeAsync(It.IsAny<GeocodeQuery>(), It.IsAny<CancellationToken>()))
            .Callback((GeocodeQuery query, CancellationToken _) => asked = query)
            .ReturnsAsync(new GeocodeResponse
            {
                Provider = "AzureMaps",
                Candidates = [new GeocodeCandidate { Point = new GeoPoint(30.2672, -97.7431), Confidence = 0.912, FormattedAddress = "100 Congress Ave, Austin, TX 78706", PostalCode = "78706", Country = "US" }],
            });

        // Act
        var json = await harness.RunAsync(services => InvokeJsonAsync(FindServiceTerritoryTool.TheName, services, new()
        {
            ["address"] = new { addressLine1 = "100 Congress Ave", city = "Austin", region = "TX", country = "us" },
        }), commit: false);

        // Assert
        var geocoded = json.GetProperty("geocoded");

        Assert.True(json.GetProperty("found").GetBoolean());
        Assert.Equal("Austin", json.GetProperty("territory").GetProperty("name").GetString());
        Assert.Equal("AzureMaps", geocoded.GetProperty("provider").GetString());
        Assert.Equal(30.2672, geocoded.GetProperty("latitude").GetDouble());
        Assert.Equal(0.91, geocoded.GetProperty("confidence").GetDouble());
        Assert.Equal("US", asked.Address.Country);
        Assert.Equal("US", asked.CountryHint);
    }

    [Fact]
    public async Task FindServiceTerritory_ByAddress_NeedsTheGeoSettingsPermission()
    {
        // Arrange
        await using var harness = await CreateAsync(authorize: (asked, _) => asked.Name != "ManageGeoSettings");

        // Act
        var result = await harness.RunAsync(services => InvokeAsync(FindServiceTerritoryTool.TheName, services, new() { ["text"] = "100 Congress Ave, Austin" }), commit: false);

        // Assert
        Assert.StartsWith("Nothing was searched: finding the territory of a street address geocodes it", result, StringComparison.Ordinal);
        _geoService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FindServiceTerritory_SaysWhenNoProviderIsSetUp_AndHidesKeysInProviderErrors()
    {
        // Arrange
        await using var harness = await CreateAsync();

        _geoService.SetupSequence(service => service.GeocodeAsync(It.IsAny<GeocodeQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodeResponse())
            .ThrowsAsync(new GeoProviderException("Google Maps refused the request (https://maps.example/geocode?address=Austin&key=SECRET-KEY-123)."));

        // Act
        var none = await harness.RunAsync(services => InvokeAsync(FindServiceTerritoryTool.TheName, services, new() { ["text"] = "100 Congress Ave, Austin" }), commit: false);
        var failed = await harness.RunAsync(services => InvokeAsync(FindServiceTerritoryTool.TheName, services, new() { ["text"] = "100 Congress Ave, Austin" }), commit: false);
        var both = await harness.RunAsync(services => InvokeAsync(FindServiceTerritoryTool.TheName, services, new() { ["text"] = "Austin", ["latitude"] = 1.0, ["longitude"] = 2.0 }), commit: false);

        // Assert
        Assert.StartsWith("Nothing was searched: no geocoding provider is set up", none, StringComparison.Ordinal);
        Assert.StartsWith("Nothing was searched: the maps provider could not answer:", failed, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET-KEY-123", failed, StringComparison.Ordinal);
        Assert.Contains("key=[hidden]", failed, StringComparison.Ordinal);
        Assert.StartsWith("Nothing was searched: give either an address", both, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindServiceTerritory_RefusesAKeyArgument()
    {
        // Arrange
        await using var harness = await CreateAsync();

        // Act
        var result = await harness.RunAsync(services => InvokeAsync(FindServiceTerritoryTool.TheName, services, new() { ["text"] = "Austin", ["apiKey"] = "SECRET" }), commit: false);

        // Assert
        Assert.StartsWith("Nothing was done: 'apiKey' is not an argument of findServiceTerritory.", result, StringComparison.Ordinal);
        _geoService.VerifyNoOtherCalls();
    }

    private static Task<string> CreateTerritoryAsync(CalendarTestHarness harness, string name, string parentId = null, Dictionary<string, string[]> members = null)
    {
        return harness.RunAsync(async services =>
        {
            var arguments = new Dictionary<string, object> { ["name"] = name, ["postalCodes"] = new[] { "7870" + name.Length } };

            if (parentId is not null)
            {
                arguments["parentId"] = parentId;
            }

            if (members is not null)
            {
                arguments["members"] = members;
            }

            var json = await InvokeJsonAsync(SaveServiceTerritoryTool.TheName, services, arguments);

            return json.GetProperty("territory").GetProperty("id").GetString();
        });
    }

    private Task<CalendarTestHarness> CreateAsync(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        return CalendarTestHarness.CreateAsync(
            services =>
            {
                var settingsService = new Mock<IGeoSettingsService>();
                settingsService.Setup(service => service.GetAsync()).ReturnsAsync(() => _settings);
                services.AddSingleton(settingsService.Object);
                services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
                services.AddGeoCore();

                foreach (var descriptor in AgentToolTestContext.CreateServices(signedIn, authorize))
                {
                    services.Add(descriptor);
                }

                services.AddSingleton(_geoService.Object);

                var timeZones = new Mock<ITimeZoneSelectListProvider>();
                timeZones.Setup(provider => provider.GetTimeZoneSelectListAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new[] { new KeyValuePair<string, string>("America/Chicago", "Central Time (Chicago)") });
                services.AddSingleton(timeZones.Object);

                services.AddSingleton<ITerritoryMemberSource>(new TestMemberSource("Team", "Teams", ("team-1", "Team One"), ("team-2", "Team Two")));
                services.AddSingleton<ITerritoryMemberSource>(new TestMemberSource("Worker", "Technicians", ("worker-1", "Sam Carter")));
            },
            GeoIndexSchema.CreateAsync,
            [new ServiceTerritoryIndexProvider(), new GeocodeRequestIndexProvider()],
            [GeoConstants.CollectionName]);
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            ListServiceTerritoriesTool.TheName => new ListServiceTerritoriesTool(),
            SaveServiceTerritoryTool.TheName => new SaveServiceTerritoryTool(),
            DeleteServiceTerritoryTool.TheName => new DeleteServiceTerritoryTool(),
            FindServiceTerritoryTool.TheName => new FindServiceTerritoryTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName)),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }

    private static async Task<JsonElement> InvokeJsonAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
        => AgentToolTestContext.ReadJson(await InvokeAsync(toolName, services, arguments));

    private sealed class TestMemberSource : ITerritoryMemberSource
    {
        private readonly (string Id, string Name)[] _members;

        public TestMemberSource(string kind, string displayName, params (string Id, string Name)[] members)
        {
            Kind = kind;
            DisplayName = displayName;
            _members = members;
        }

        public string Kind { get; }

        public string DisplayName { get; }

        public Task<IReadOnlyList<(string Id, string Name)>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<(string Id, string Name)>>(_members);
    }
}
