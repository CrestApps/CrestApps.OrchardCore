using System.Net;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Four services each built their own <see cref="HttpClient"/>, set their own base address and bearer header, and
/// parsed the provider's answer their own way. Four copies means four places to fix a bug and four chances for
/// them to disagree about what a 429 means — and none of them retried, so a rate-limited command was simply a
/// call that did not happen. This is the one place that talks to Telnyx.
/// </summary>
public sealed class TelnyxApiClientTests
{
    [Fact]
    public async Task Answer_PostsToTheCallActionPath_WithTheBearer()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().RespondWith(HttpStatusCode.OK, """{"data":{"call_control_id":"ctrl-1"}}""");
        var client = CreateClient(handler);

        // Act
        var result = await client.AnswerAsync("ctrl-1", clientState: "state-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v2/calls/ctrl-1/actions/answer", request.Path);
        Assert.Equal("Bearer test-api-key", request.Authorization);
        Assert.Contains("client_state", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Answer_EscapesACallIdentifierWithPathCharactersInIt()
    {
        // Arrange
        // Call control identifiers are opaque provider strings. One containing a slash would otherwise be sent as
        // a different path entirely — a command silently issued against the wrong resource.
        var handler = new RecordingHttpMessageHandler().RespondWith(HttpStatusCode.OK);
        var client = CreateClient(handler);

        // Act
        await client.AnswerAsync("ctrl/with/slashes", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("/v2/calls/ctrl%2Fwith%2Fslashes/actions/answer", handler.Requests[0].Path);
    }

    [Fact]
    public async Task Originate_PostsTheCallAndReturnsTheProviderCallId()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().RespondWith(HttpStatusCode.OK, """{"data":{"call_control_id":"ctrl-new"}}""");
        var client = CreateClient(handler);

        // Act
        var result = await client.OriginateAsync(
            new TelnyxOriginateRequest
            {
                To = "+16502530001",
                From = "+16502530000",
                ConnectionId = "conn-1",
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("ctrl-new", result.CallControlId);
        Assert.Equal("/v2/calls", handler.Requests[0].Path);
    }

    [Fact]
    public async Task AFailedCommand_CarriesTheStatusAndBody_RatherThanThrowing()
    {
        // Arrange
        // The callers of this client are handling a live call. An exception on a 422 would abandon a customer
        // mid-flow; a result they can inspect lets them fail the command and tell the agent why.
        var handler = new RecordingHttpMessageHandler().RespondWith(HttpStatusCode.UnprocessableEntity, """{"errors":[{"detail":"bad request"}]}""");
        var client = CreateClient(handler);

        // Act
        var result = await client.AnswerAsync("ctrl-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, result.StatusCode);
        Assert.Contains("bad request", result.ErrorBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARateLimitedCommand_IsRetried_AndSucceedsOnTheSecondAttempt()
    {
        // Arrange
        // Telnyx rate limits per account, so a busy tenant hits 429 during a burst of call control. Not retrying
        // means the answer or the bridge simply does not happen and the customer hears silence.
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.TooManyRequests, "{}", retryAfter: TimeSpan.Zero)
            .RespondWith(HttpStatusCode.OK, """{"data":{"call_control_id":"ctrl-1"}}""");

        var client = CreateClient(handler);

        // Act
        var result = await client.AnswerAsync("ctrl-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task AServerError_IsRetried()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.InternalServerError)
            .RespondWith(HttpStatusCode.OK, """{"data":{"call_control_id":"ctrl-1"}}""");

        var client = CreateClient(handler);

        // Act
        var result = await client.AnswerAsync("ctrl-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ARejectedCommand_IsNotRetried()
    {
        // Arrange
        // A 422 means the provider understood and refused. Retrying a refusal wastes a rate-limit budget the
        // next real command needs, and on a non-idempotent command it risks doing the thing twice.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.UnprocessableEntity);
        var client = CreateClient(handler);

        // Act
        await client.AnswerAsync("ctrl-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Originate_IsNotRetried_HoweverTheProviderFails()
    {
        // Arrange
        // Placing a call is not idempotent. A retry that the provider had in fact accepted would dial the
        // customer twice, which is worse than the command failing.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.InternalServerError);
        var client = CreateClient(handler);

        // Act
        var result = await client.OriginateAsync(
            new TelnyxOriginateRequest { To = "+16502530001", From = "+16502530000", ConnectionId = "conn-1" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ATransportFailure_IsAResult_NotAnException()
    {
        // Arrange
        var handler = new ThrowingHttpMessageHandler();
        var client = CreateClient(handler);

        // Act
        var result = await client.AnswerAsync("ctrl-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorBody));
    }

    [Fact]
    public async Task Speak_Playback_Gather_And_Transfer_EachPostToTheirOwnAction()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var client = CreateClient(handler);

        // Act
        await client.SpeakAsync("ctrl-1", "hello", "female", "en-US", cancellationToken: TestContext.Current.CancellationToken);
        await client.PlaybackAsync("ctrl-1", "https://example.com/hold.mp3", cancellationToken: TestContext.Current.CancellationToken);
        await client.GatherAsync("ctrl-1", "Press 1", "1", cancellationToken: TestContext.Current.CancellationToken);
        await client.TransferAsync("ctrl-1", "+16502530002", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("/v2/calls/ctrl-1/actions/speak", handler.Requests[0].Path);
        Assert.Equal("/v2/calls/ctrl-1/actions/playback_start", handler.Requests[1].Path);
        Assert.Equal("/v2/calls/ctrl-1/actions/gather_using_speak", handler.Requests[2].Path);
        Assert.Equal("/v2/calls/ctrl-1/actions/transfer", handler.Requests[3].Path);
    }

    [Fact]
    public async Task Credentials_AreCreatedAndDeletedOnTheCredentialResource()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, """{"data":{"id":"cred-1","sip_username":"user-1"}}""")
            .RespondWith(HttpStatusCode.OK);

        var client = CreateClient(handler);

        // Act
        var created = await client.CreateCredentialAsync("conn-1", "agent-1", cancellationToken: TestContext.Current.CancellationToken);
        await client.DeleteCredentialAsync("cred-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(created.Succeeded);
        Assert.Equal("cred-1", created.CredentialId);
        Assert.Equal("user-1", created.SipUsername);
        Assert.Equal("/v2/telephony_credentials", handler.Requests[0].Path);
        Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
        Assert.Equal("/v2/telephony_credentials/cred-1", handler.Requests[1].Path);
    }

    [Fact]
    public async Task DeletingACredentialTheProviderHasAlreadyForgotten_IsSuccess()
    {
        // Arrange
        // Revocation runs on sign-out and on a cap eviction, both of which can race a credential that already
        // expired provider-side. Treating 404 as failure would leave a local record nobody can ever clean up.
        var handler = new RecordingHttpMessageHandler().RespondWith(HttpStatusCode.NotFound);
        var client = CreateClient(handler);

        // Act
        var result = await client.DeleteCredentialAsync("cred-gone", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
    }

    private static TelnyxApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.telnyx.com/v2/"),
        };

        var options = new OptionsWrapper<TelnyxOptions>(new TelnyxOptions
        {
            ApiBaseUrl = "https://api.telnyx.com/v2/",
            ApiKey = "test-api-key",
        });

        return new TelnyxApiClient(
            httpClient,
            options,
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("The provider could not be reached.");
    }
}
