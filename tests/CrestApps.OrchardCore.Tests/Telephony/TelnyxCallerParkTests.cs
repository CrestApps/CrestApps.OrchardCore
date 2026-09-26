using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Taking a Contact Center caller out of the agent's bridge on Telnyx, so the agent's leg can be hung up while the
/// caller stays on the line. The bridge is issued on the agent's leg with <c>park_after_unbridge=self</c>, which parks
/// only that leg; the caller's leg keeps Telnyx's default and is hung up when the bridge ends with the agent's hangup.
/// </summary>
public sealed class TelnyxCallerParkTests
{
    [Fact]
    public async Task ParkCallerAsync_MovesTheCallerIntoAConferenceOfTheirOwn_AndLeavesIt()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{\"id\":\"conf-1\"}}");
        var provider = TelnyxContactCenterProviderFactory.Create(handler);

        // Act
        var parked = await provider.ParkCallerAsync("caller-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(parked);
        Assert.Equal(2, handler.Requests.Count);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.EndsWith("/v2/conferences", handler.Requests[0].RequestUri.AbsolutePath, StringComparison.Ordinal);

        using (var create = JsonDocument.Parse(handler.RequestBodies[0]))
        {
            var name = create.RootElement.GetProperty("name").GetString();
            Assert.Equal("caller-1", create.RootElement.GetProperty("call_control_id").GetString());
            Assert.StartsWith("cc-park-", name, StringComparison.Ordinal);

            // A later transfer of the same call is a new park, never collapsed into this one.
            Assert.Equal(name, create.RootElement.GetProperty("command_id").GetString());
        }

        Assert.EndsWith("/v2/conferences/conf-1/actions/leave", handler.Requests[1].RequestUri.AbsolutePath, StringComparison.Ordinal);

        using (var leave = JsonDocument.Parse(handler.RequestBodies[1]))
        {
            Assert.Equal("caller-1", leave.RootElement.GetProperty("call_control_id").GetString());
        }

        Assert.DoesNotContain(handler.Requests, request => request.RequestUri.AbsolutePath.EndsWith("/actions/hangup", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParkCallerAsync_WhenTheConferenceIsRefused_SaysTheCallerIsStillInTheBridge()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.UnprocessableEntity, "{\"errors\":[{\"code\":\"90018\"}]}");
        var provider = TelnyxContactCenterProviderFactory.Create(handler);

        // Act
        var parked = await provider.ParkCallerAsync("caller-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(parked);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ParkCallerAsync_WhenLeavingTheConferenceIsRefused_TheCallerIsStillOutOfTheAgentsBridge()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(request => new HttpResponseMessage(
            request.RequestUri.AbsolutePath.EndsWith("/leave", StringComparison.Ordinal)
                ? HttpStatusCode.UnprocessableEntity
                : HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":{\"id\":\"conf-1\"}}"),
        });
        var provider = TelnyxContactCenterProviderFactory.Create(handler);

        // Act
        var parked = await provider.ParkCallerAsync("caller-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(parked);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ParkCallerAsync_WithoutACall_SendsNothing()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{\"id\":\"conf-1\"}}");
        var provider = TelnyxContactCenterProviderFactory.Create(handler);

        // Act
        var parked = await provider.ParkCallerAsync(" ", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(parked);
        Assert.Empty(handler.Requests);
    }
}
