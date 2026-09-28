using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Asterisk.Web;
using CrestApps.OrchardCore.Asterisk.Web.Hubs;
using CrestApps.OrchardCore.Asterisk.Web.Models;
using CrestApps.OrchardCore.Asterisk.Web.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskTwoPartyCallSimulatorServiceTests
{
    [Fact]
    public async Task SimulateAsync_WhenBothPartiesEnterStasis_ShouldCreateMixingBridge()
    {
        // Arrange
        var handler = new TwoPartyAriHandler();
        var service = CreateService(handler);
        var input = new TwoPartyCallSimulationInputModel
        {
            PartyAEndpoint = "Local/2001@crestapps-simulation",
            PartyACallerId = "Party A <2001>",
            PartyBEndpoint = "Local/2002@crestapps-simulation",
            PartyBCallerId = "Party B <2002>",
        };

        // Act
        var result = await service.SimulateAsync(input, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("bridge-1", result.BridgeId);
        Assert.Equal("channel-a", result.PartyAChannelId);
        Assert.Equal("channel-b", result.PartyBChannelId);
        Assert.Equal("channel-a,channel-b", handler.AddedChannels);
        Assert.Equal("mixing", handler.BridgeType);
    }

    [Fact]
    public async Task SimulateAsync_WhenBridgeAttachmentFails_ShouldCleanUpCreatedResources()
    {
        // Arrange
        var handler = new TwoPartyAriHandler
        {
            FailBridgeAttachment = true,
        };
        var service = CreateService(handler);
        var input = new TwoPartyCallSimulationInputModel
        {
            PartyAEndpoint = "Local/2001@crestapps-simulation",
            PartyBEndpoint = "Local/2002@crestapps-simulation",
        };

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.SimulateAsync(input, TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("channel-a", handler.DeletedResources);
        Assert.Contains("channel-b", handler.DeletedResources);
        Assert.Contains("bridge-1", handler.DeletedResources);
    }

    [Fact]
    public async Task SimulateAsync_WhenOneOriginationFails_ShouldCleanUpSuccessfulOrigination()
    {
        // Arrange
        var handler = new TwoPartyAriHandler
        {
            FailPartyBOrigination = true,
        };
        var service = CreateService(handler);
        var input = new TwoPartyCallSimulationInputModel
        {
            PartyAEndpoint = "Local/2001@crestapps-simulation",
            PartyBEndpoint = "Local/2002@crestapps-simulation",
        };

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.SimulateAsync(input, TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("channel-a", handler.DeletedResources);
    }

    private static AsteriskTwoPartyCallSimulatorService CreateService(HttpMessageHandler handler)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler, disposeHandler: false));

        var options = Options.Create(new AsteriskWebOptions
        {
            AsteriskBaseUrl = "http://asterisk.test/ari/",
            AsteriskUserName = "user",
            AsteriskPassword = "password",
            AsteriskApplicationName = "crestapps-dashboard",
            AsteriskTimeoutSeconds = 5,
        });
        var diagnosticsService = new AsteriskDiagnosticsService(
            httpClientFactory.Object,
            options,
            TimeProvider.System,
            NullLogger<AsteriskDiagnosticsService>.Instance);
        var broadcastService = new AsteriskDashboardBroadcastService(
            diagnosticsService,
            Mock.Of<IHubContext<AsteriskDashboardHub>>(),
            TimeProvider.System,
            NullLogger<AsteriskDashboardBroadcastService>.Instance);

        return new AsteriskTwoPartyCallSimulatorService(
            httpClientFactory.Object,
            broadcastService,
            options,
            TimeProvider.System,
            NullLogger<AsteriskTwoPartyCallSimulatorService>.Instance);
    }

    private sealed class TwoPartyAriHandler : HttpMessageHandler
    {
        private readonly ConcurrentDictionary<string, string> _channelArguments = new(StringComparer.Ordinal);
        public bool FailBridgeAttachment { get; set; }

        public bool FailPartyBOrigination { get; set; }

        public string AddedChannels { get; private set; }

        public string BridgeType { get; private set; }

        public ConcurrentBag<string> DeletedResources { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri.AbsolutePath.Trim('/');
            var query = QueryHelpers.ParseQuery(request.RequestUri.Query);

            if (request.Method == HttpMethod.Post && path == "ari/channels")
            {
                if (FailPartyBOrigination &&
                    query["endpoint"].ToString().Contains("2002", StringComparison.Ordinal))
                {
                    return Json(HttpStatusCode.InternalServerError, "originate failed");
                }

                var channelId = query["endpoint"].ToString().Contains("2001", StringComparison.Ordinal)
                    ? "channel-a"
                    : "channel-b";
                _channelArguments[channelId] = query["appArgs"].ToString();

                return Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { id = channelId }));
            }

            if (request.Method == HttpMethod.Get && path.StartsWith("ari/channels/", StringComparison.Ordinal))
            {
                var channelId = path.Substring("ari/channels/".Length);
                var appArguments = _channelArguments[channelId];

                return Json(
                    HttpStatusCode.OK,
                    JsonSerializer.Serialize(new
                    {
                        id = channelId,
                        dialplan = new
                        {
                            app_name = "Stasis",
                            app_data = $"crestapps-dashboard,{appArguments}",
                        },
                    }));
            }

            if (request.Method == HttpMethod.Post && path == "ari/bridges")
            {
                BridgeType = query["type"].ToString();

                return Json(HttpStatusCode.OK, """{"id":"bridge-1"}""");
            }

            if (request.Method == HttpMethod.Post && path == "ari/bridges/bridge-1/addChannel")
            {
                AddedChannels = query["channel"].ToString();

                return Json(
                    FailBridgeAttachment ? HttpStatusCode.InternalServerError : HttpStatusCode.NoContent,
                    FailBridgeAttachment ? "bridge failed" : string.Empty);
            }

            if (request.Method == HttpMethod.Delete)
            {
                DeletedResources.Add(path.Substring(path.LastIndexOf('/') + 1));

                return Json(HttpStatusCode.NoContent, string.Empty);
            }

            return Json(HttpStatusCode.NotFound, string.Empty);
        }

        private static Task<HttpResponseMessage> Json(HttpStatusCode statusCode, string content)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            });
        }
    }
}
