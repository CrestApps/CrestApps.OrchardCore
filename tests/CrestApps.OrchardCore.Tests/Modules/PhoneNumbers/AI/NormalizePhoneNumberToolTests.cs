using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.PhoneNumbers.AI;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Modules.PhoneNumbers.AI;

/// <summary>
/// The phone number check parses a number with the platform's phone number service for a signed-in caller and returns
/// the value to store the way every editor normalizes it.
/// </summary>
public sealed class NormalizePhoneNumberToolTests
{
    [Fact]
    public async Task NormalizePhoneNumber_RefusesACallerWhoIsNotSignedIn()
    {
        // Arrange
        var services = Build(signedIn: false);

        // Act
        var result = await InvokeAsync(services, new() { ["phoneNumber"] = "+1 650 253 0000" });

        // Assert
        Assert.Equal(NormalizePhoneNumberTool.NotAllowedMessage, result);
    }

    [Fact]
    public async Task NormalizePhoneNumber_ReturnsTheE164FormRegionAndTimeZones()
    {
        // Arrange
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(services, new() { ["phoneNumber"] = "(650) 253-0000", ["regionCode"] = "us" }));

        // Assert
        Assert.True(json.GetProperty("valid").GetBoolean());
        Assert.Equal("+16502530000", json.GetProperty("e164").GetString());
        Assert.Equal("+16502530000", json.GetProperty("valueToStore").GetString());
        Assert.Equal("US", json.GetProperty("region").GetString());
        Assert.Equal("+1", json.GetProperty("countryCallingCode").GetString());
        Assert.NotEmpty(json.GetProperty("timeZones").EnumerateArray());
    }

    [Fact]
    public async Task NormalizePhoneNumber_SaysWhenTheNumberIsNotValid()
    {
        // Arrange
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(services, new() { ["phoneNumber"] = "123", ["regionCode"] = "US" }));

        // Assert
        Assert.False(json.GetProperty("valid").GetBoolean());
        Assert.Equal("123", json.GetProperty("valueToStore").GetString());
    }

    [Fact]
    public async Task NormalizePhoneNumber_RefusesAnUnknownRegion()
    {
        // Arrange
        var services = Build();

        // Act
        var result = await InvokeAsync(services, new() { ["phoneNumber"] = "555 0100", ["regionCode"] = "ZZ" });

        // Assert
        Assert.StartsWith("Nothing was checked: 'ZZ'", result, StringComparison.Ordinal);
    }

    private static ServiceProvider Build(bool signedIn = true)
    {
        var services = AgentToolTestContext.CreateServices(signedIn);

        services.AddSingleton<IPhoneNumberService>(new DefaultPhoneNumberService());

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(IServiceProvider services, Dictionary<string, object> arguments)
        => AgentToolTestContext.InvokeAsync(new NormalizePhoneNumberTool(), services, arguments);
}
