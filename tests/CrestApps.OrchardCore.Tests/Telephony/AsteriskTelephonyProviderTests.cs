using System.Net;
using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskTelephonyProviderTests
{
    private const string PlainPassword = "secret-password";
    private const string BaseUrl = "http://asterisk.test:8088/ari/";

    [Fact]
    public async Task DialAsync_WhenConfigured_PostsToAsteriskAriWithBasicAuthentication()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"id\":\"call-1\"}");
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var result = await provider.DialAsync(new DialRequest { To = "+15551234567" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Call);
        Assert.Equal("call-1", result.Call.CallId);
        Assert.Equal(CallState.Connecting, result.Call.State);
        Assert.Equal(CallDirection.Outbound, result.Call.Direction);

        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal($"{BaseUrl}channels?endpoint=PJSIP%2F%2B15551234567@phones&timeout=30&app=crestapps-telephony&callerId=%2B15550000000", handler.LastRequest.RequestUri.AbsoluteUri);
        Assert.Equal("Basic", handler.LastRequest.Headers.Authorization.Scheme);
        Assert.Equal(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"ari-user:{PlainPassword}")), handler.LastRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task DialAsync_WhenUsingLocalEndpoint_ReturnsConnectingUntilProviderEventArrives()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"id\":\"call-1\"}");
        var provider = CreateProvider(handler, out _, isEnabled: true, endpointTemplate: "Local/{number}@default");

        // Act
        var result = await provider.DialAsync(new DialRequest { To = "1000" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Call);
        Assert.Equal(CallState.Connecting, result.Call.State);
        Assert.Equal(
            $"{BaseUrl}channels?endpoint=Local%2F1000@default&timeout=30&app=crestapps-telephony&callerId=%2B15550000000",
            handler.LastRequest.RequestUri.AbsoluteUri);
    }

    [Fact]
    public async Task DialAsync_WhenDisabled_ReturnsFailedAndDoesNotCallApi()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"id\":\"call-1\"}");
        var provider = CreateProvider(handler, out _, isEnabled: false);

        // Act
        var result = await provider.DialAsync(new DialRequest { To = "+15551234567" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task GetClientCredentialsAsync_WhenConfigured_ReturnsProviderName()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var credentials = await provider.GetClientCredentialsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(credentials);
        Assert.Equal(AsteriskConstants.ProviderTechnicalName, credentials.ProviderName);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task AnswerAsync_WhenConfigured_PostsToAnswerEndpoint()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var result = await provider.AnswerAsync(new CallReference { CallId = "call-1" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Call);
        Assert.Equal(CallState.Connected, result.Call.State);
        Assert.Equal(CallDirection.Inbound, result.Call.Direction);
        Assert.Equal($"{BaseUrl}channels/call-1/answer", handler.LastRequest.RequestUri.AbsoluteUri);
    }

    [Fact]
    public async Task RejectAsync_WhenConfigured_DeletesChannel()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var result = await provider.RejectAsync(new CallReference { CallId = "call-1" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Call);
        Assert.Equal(CallState.Disconnected, result.Call.State);
        Assert.Equal(CallDirection.Inbound, result.Call.Direction);
        Assert.Equal($"{BaseUrl}channels/call-1", handler.LastRequest.RequestUri.AbsoluteUri);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest.Method);
    }

    [Fact]
    public async Task HangupAsync_WhenChannelAlreadyMissing_ReturnsDisconnectedSuccess()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.NotFound, """{"message":"Channel not found"}""");
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var result = await provider.HangupAsync(
            new CallReference { CallId = "call-1" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Call);
        Assert.Equal(CallState.Disconnected, result.Call.State);
        Assert.Equal($"{BaseUrl}channels/call-1", handler.LastRequest.RequestUri.AbsoluteUri);
    }

    [Fact]
    public async Task MergeThenHangupAll_WhenConferenceBecomesEmpty_DeletesOwnedBridge()
    {
        // Arrange
        var bridgeLookupCount = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            var url = request.RequestUri.AbsoluteUri;

            if (request.Method == HttpMethod.Post && url == $"{BaseUrl}bridges?type=mixing")
            {
                return JsonResponse("""{"id":"bridge-1"}""");
            }

            if (request.Method == HttpMethod.Get &&
                url.EndsWith($"/variable?variable={AsteriskConstants.ConferenceBridgeVariableName}", StringComparison.Ordinal))
            {
                return JsonResponse("""{"value":"bridge-1"}""");
            }

            if (request.Method == HttpMethod.Get && url == $"{BaseUrl}bridges/bridge-1")
            {
                bridgeLookupCount++;

                return JsonResponse(bridgeLookupCount switch
                {
                    1 => """{"id":"bridge-1","channels":["call-2","call-3"]}""",
                    2 => """{"id":"bridge-1","channels":["call-3"]}""",
                    _ => """{"id":"bridge-1","channels":[]}""",
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var mergeResult = await provider.MergeAsync(
            new MergeRequest
            {
                CallIds = ["call-1", "call-2", "call-3"],
            },
            TestContext.Current.CancellationToken);
        var firstHangup = await provider.HangupAsync(
            new CallReference { CallId = "call-1" },
            TestContext.Current.CancellationToken);
        var secondHangup = await provider.HangupAsync(
            new CallReference { CallId = "call-2" },
            TestContext.Current.CancellationToken);
        var thirdHangup = await provider.HangupAsync(
            new CallReference { CallId = "call-3" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(mergeResult.Succeeded);
        Assert.True(firstHangup.Succeeded);
        Assert.True(secondHangup.Succeeded);
        Assert.True(thirdHangup.Succeeded);
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Post &&
                request.RequestUri.AbsoluteUri ==
                $"{BaseUrl}bridges/bridge-1/addChannel?channel=call-1,call-2,call-3");
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Post &&
                request.RequestUri.AbsoluteUri ==
                $"{BaseUrl}channels/call-1/variable?variable={AsteriskConstants.ConferenceBridgeVariableName}&value=bridge-1");
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Post &&
                request.RequestUri.AbsoluteUri ==
                $"{BaseUrl}channels/call-2/variable?variable={AsteriskConstants.ConferenceBridgeVariableName}&value=bridge-1");
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Post &&
                request.RequestUri.AbsoluteUri ==
                $"{BaseUrl}channels/call-1/variable?variable={AsteriskConstants.HoldStateVariableName}&value=False");
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Post &&
                request.RequestUri.AbsoluteUri ==
                $"{BaseUrl}channels/call-2/variable?variable={AsteriskConstants.HoldStateVariableName}&value=False");
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Post &&
                request.RequestUri.AbsoluteUri ==
                $"{BaseUrl}channels/call-3/variable?variable={AsteriskConstants.HoldStateVariableName}&value=False");
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Delete &&
                request.RequestUri.AbsoluteUri == $"{BaseUrl}bridges/bridge-1");
    }

    [Fact]
    public async Task MergeAsync_WhenCleanupTrackingFails_KeepsSuccessfulConference()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(request =>
        {
            var url = request.RequestUri.AbsoluteUri;

            if (request.Method == HttpMethod.Post && url == $"{BaseUrl}bridges?type=mixing")
            {
                return JsonResponse("""{"id":"bridge-1"}""");
            }

            if (url.Contains("channels/call-1/variable", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var result = await provider.MergeAsync(
            new MergeRequest
            {
                PrimaryCallId = "call-1",
                SecondaryCallId = "call-2",
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Delete &&
                request.RequestUri.AbsoluteUri == $"{BaseUrl}bridges/bridge-1");
    }

    [Fact]
    public void Capabilities_WhenUsingLocalLoopback_KeepAdvancedActionsEnabled()
    {
        // Arrange
        var provider = CreateProvider(
            new StubHttpMessageHandler(HttpStatusCode.OK),
            out _,
            isEnabled: true,
            endpointTemplate: "Local/{number}@default");

        // Act
        var capabilities = provider.Capabilities;

        // Assert
        Assert.True(capabilities.HasFlag(TelephonyCapabilities.Dial));
        Assert.True(capabilities.HasFlag(TelephonyCapabilities.Hangup));
        Assert.True(capabilities.HasFlag(TelephonyCapabilities.SendDigits));
        Assert.True(capabilities.HasFlag(TelephonyCapabilities.ReceiveCalls));
        Assert.True(capabilities.HasFlag(TelephonyCapabilities.Hold));
        Assert.True(capabilities.HasFlag(TelephonyCapabilities.Resume));
        Assert.True(capabilities.HasFlag(TelephonyCapabilities.Mute));
        Assert.True(capabilities.HasFlag(TelephonyCapabilities.Transfer));
        Assert.True(capabilities.HasFlag(TelephonyCapabilities.Merge));
        Assert.True(capabilities.HasFlag(TelephonyCapabilities.Voicemail));
        Assert.True(capabilities.HasFlag(TelephonyCapabilities.Directory));
    }

    [Fact]
    public async Task GetDirectoryAsync_WhenConfigured_MapsAsteriskEndpoints()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            """
            [
              {
                "technology": "PJSIP",
                "resource": "2001",
                "state": "online",
                "channel_ids": []
              },
              {
                "technology": "PJSIP",
                "resource": "sales",
                "state": "offline",
                "channel_ids": []
              }
            ]
            """);
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var result = await provider.GetDirectoryAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Entries.Count);
        Assert.Equal("2001", result.Entries[0].Destination);
        Assert.Equal("2001", result.Entries[0].Extension);
        Assert.Equal($"{BaseUrl}endpoints", handler.LastRequest.RequestUri.AbsoluteUri);
    }

    [Fact]
    public void Capabilities_WhenUsingStasisEndpoint_KeepAdvancedActionsEnabled()
    {
        // Arrange
        var provider = CreateProvider(
            new StubHttpMessageHandler(HttpStatusCode.OK),
            out _,
            isEnabled: true,
            endpointTemplate: "PJSIP/{number}@phones");

        // Act
        var capabilities = provider.Capabilities;

        // Assert
        Assert.True(capabilities.HasFlag(TelephonyCapabilities.Hold));
        Assert.True(capabilities.HasFlag(TelephonyCapabilities.Resume));
        Assert.True(capabilities.HasFlag(TelephonyCapabilities.Mute));
        Assert.True(capabilities.HasFlag(TelephonyCapabilities.Merge));
        Assert.True(capabilities.HasFlag(TelephonyCapabilities.ReceiveCalls));
        Assert.False(capabilities.HasFlag(TelephonyCapabilities.Transfer));
        Assert.True(capabilities.HasFlag(TelephonyCapabilities.Voicemail));
    }

    [Fact]
    public async Task TransferAsync_WhenUsingLocalLoopback_ContinuesChannelToTargetExtension()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateProvider(handler, out _, isEnabled: true, endpointTemplate: "Local/{number}@default");

        // Act
        var result = await provider.TransferAsync(
            new TransferRequest
            {
                CallId = "call-1",
                To = "2000",
                Mode = TransferMode.Blind,
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Call);
        Assert.Equal(CallState.Disconnected, result.Call.State);
        Assert.Equal(
            $"{BaseUrl}channels/call-1/continue?context=default&extension=2000&priority=1",
            handler.LastRequest.RequestUri.AbsoluteUri);
    }

    [Fact]
    public async Task SendToVoicemailAsync_WhenConfigured_SetsMetadataAndContinuesToResolvedExtension()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateProvider(handler, out _, isEnabled: true, endpointTemplate: "Local/{number}@default");

        // Act
        var result = await provider.SendToVoicemailAsync(
            new CallReference
            {
                CallId = "call-1",
                Metadata = new Dictionary<string, object>
                {
                    ["voicemailRecipientUserName"] = "mike",
                    ["queueName"] = "Support",
                },
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Call);
        Assert.Equal(CallState.Disconnected, result.Call.State);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal($"{BaseUrl}channels/call-1/variable?variable=CRESTAPPS_METADATA_VOICEMAILRECIPIENTUSERNAME&value=mike", handler.Requests[0].RequestUri.AbsoluteUri);
        Assert.Equal($"{BaseUrl}channels/call-1/variable?variable=CRESTAPPS_METADATA_QUEUENAME&value=Support", handler.Requests[1].RequestUri.AbsoluteUri);
        Assert.Equal($"{BaseUrl}channels/call-1/continue?context=voicemail&extension=mike&priority=1", handler.Requests[2].RequestUri.AbsoluteUri);
    }

    [Fact]
    public async Task GetCallAsync_WhenChannelIsHeldAndMuted_RecoversGranularProviderState()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(request =>
        {
            var body = request.RequestUri.AbsoluteUri switch
            {
                $"{BaseUrl}channels/call-1" =>
                    """
                    {
                      "id": "call-1",
                      "state": "Up",
                      "caller": { "number": "+15550001000" },
                      "connected": { "number": "+15550002000" }
                    }
                    """,
                $"{BaseUrl}channels/call-1/variable?variable=CRESTAPPS_STATE_ONHOLD" => """{"value":"true"}""",
                $"{BaseUrl}channels/call-1/variable?variable=CRESTAPPS_STATE_MUTED" => """{"value":"true"}""",
                $"{BaseUrl}channels/call-1/variable?variable=CRESTAPPS_CONFERENCE_BRIDGE_ID" => """{"value":""}""",
                _ => throw new InvalidOperationException($"Unexpected request: {request.RequestUri}"),
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body),
            };
        });
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var result = await provider.GetCallStateAsync("call-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.Found);
        var call = result.Call;
        Assert.NotNull(call);
        Assert.Equal(CallState.OnHold, call.State);
        Assert.True(call.IsMuted);
        Assert.Equal(5, handler.Requests.Count);
    }

    [Fact]
    public async Task GetCallAsync_WhenChannelDisappearsDuringLookup_ReturnsNotFound()
    {
        // Arrange
        var channelRequestCount = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri.AbsoluteUri == $"{BaseUrl}channels/call-1")
            {
                channelRequestCount++;

                if (channelRequestCount > 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "id": "call-1",
                          "state": "Up"
                        }
                        """),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"value":"false"}"""),
            };
        });
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var result = await provider.GetCallStateAsync("call-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.Found);
        Assert.Null(result.Call);
        Assert.Equal(5, handler.Requests.Count);
    }

    [Fact]
    public async Task GetCallAsync_WhenAriReturnsUnknownChannelState_DoesNotAssumeConnected()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            """
            {
              "id": "call-1",
              "state": "Mystery",
              "caller": { "number": "+15550001000" },
              "connected": { "number": "+15550002000" }
            }
            """);
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var result = await provider.GetCallStateAsync("call-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(result.Call);
        Assert.Single(handler.Requests);
    }

    private static AsteriskTelephonyProvider CreateProvider(
        StubHttpMessageHandler handler,
        out IDataProtectionProvider dataProtectionProvider,
        bool isEnabled,
        string endpointTemplate = "PJSIP/{number}@phones")
    {
        dataProtectionProvider = new EphemeralDataProtectionProvider();

        var protectedPassword = dataProtectionProvider
            .CreateProtector(AsteriskConstants.ProtectorName)
            .Protect(PlainPassword);

        var settings = new AsteriskSettings
        {
            IsEnabled = isEnabled,
            BaseUrl = BaseUrl,
            UserName = "ari-user",
            Password = protectedPassword,
            ApplicationName = "crestapps-telephony",
            EndpointTemplate = endpointTemplate,
            OutboundCallerId = "+15550000000",
            TimeoutSeconds = 30,
            VoicemailContext = "voicemail",
            VoicemailExtensionTemplate = "{voicemailRecipientUserName}",
            VoicemailPriority = 1,
        };

        return new AsteriskTelephonyProvider(
            SiteServiceFactory.Create(settings),
            dataProtectionProvider,
            new StubHttpClientFactory(handler),
            new StubClock(),
            NullLogger<AsteriskTelephonyProvider>.Instance,
            new PassThroughStringLocalizer<AsteriskTelephonyProvider>());
    }

    private static HttpResponseMessage JsonResponse(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content),
        };
    }
}
