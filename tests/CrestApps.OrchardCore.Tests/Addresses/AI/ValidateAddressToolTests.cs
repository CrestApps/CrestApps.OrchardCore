using CrestApps.OrchardCore.Addresses.AI;
using CrestApps.OrchardCore.Addresses.Core;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Addresses.AI;

/// <summary>
/// The address check runs the shared address editor's rules for a signed-in caller and returns the problems in the
/// editor's own words, the tidied address, its printed lines and the country's layout.
/// </summary>
public sealed class ValidateAddressToolTests
{
    [Fact]
    public async Task ValidateAddress_RefusesACallerWhoIsNotSignedIn()
    {
        // Arrange
        var services = Build(signedIn: false);

        // Act
        var result = await InvokeAsync(services, new() { ["address"] = new { country = "US" } });

        // Assert
        Assert.Equal(ValidateAddressTool.NotAllowedMessage, result);
    }

    [Fact]
    public async Task ValidateAddress_AcceptsACompleteAddress_AndPrintsIt()
    {
        // Arrange
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(services, new()
        {
            ["address"] = new
            {
                addressLine1 = "1600 Amphitheatre Pkwy",
                city = "Mountain View",
                region = "CA",
                postalCode = "94043",
                country = "us",
            },
        }));

        // Assert
        Assert.True(json.GetProperty("valid").GetBoolean());
        Assert.Empty(json.GetProperty("problems").EnumerateArray());
        Assert.Equal("US", json.GetProperty("normalizedAddress").GetProperty("country").GetString());
        Assert.Contains(json.GetProperty("lines").EnumerateArray(), line => line.GetString().Contains("Mountain View", StringComparison.Ordinal));
        Assert.Contains(json.GetProperty("country").GetProperty("requiredFields").EnumerateArray(), field => field.GetString() == "postalCode");
    }

    [Fact]
    public async Task ValidateAddress_ReturnsTheProblemsPerField()
    {
        // Arrange
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(services, new()
        {
            ["address"] = new
            {
                addressLine1 = "1600 Amphitheatre Pkwy",
                city = "Mountain View",
                region = "CA",
                postalCode = "ABCDE",
                country = "US",
            },
        }));

        // Assert
        var problem = Assert.Single(json.GetProperty("problems").EnumerateArray());

        Assert.False(json.GetProperty("valid").GetBoolean());
        Assert.Equal("postalCode", problem.GetProperty("field").GetString());
        Assert.Equal("Invalid", problem.GetProperty("problem").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task ValidateAddress_ListsTheCountries_WhenTheCountryIsUnknown()
    {
        // Arrange
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(services, new() { ["address"] = new { city = "Atlantis", country = "ZZ" } }));

        // Assert
        Assert.False(json.GetProperty("valid").GetBoolean());
        Assert.Contains(json.GetProperty("countries").EnumerateArray(), country => country.GetProperty("code").GetString() == "US");
    }

    [Fact]
    public async Task ValidateAddress_RefusesAFieldAnAddressDoesNotHave()
    {
        // Arrange
        var services = Build();

        // Act
        var result = await InvokeAsync(services, new() { ["address"] = new { street = "1 Main St", country = "US" } });

        // Assert
        Assert.StartsWith("Nothing was checked: 'street' is not a field of the address.", result, StringComparison.Ordinal);
    }

    private static ServiceProvider Build(bool signedIn = true)
    {
        var services = AgentToolTestContext.CreateServices(signedIn);

        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
        services.AddAddressesCore();

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(IServiceProvider services, Dictionary<string, object> arguments)
        => AgentToolTestContext.InvokeAsync(new ValidateAddressTool(), services, arguments);
}
