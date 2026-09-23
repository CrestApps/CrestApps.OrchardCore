using System.Net;
using System.Text;
using CrestApps.OrchardCore.Omnichannel.Sms.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Sms;

/// <summary>
/// Recording why Twilio refused a message, which its provider otherwise discards.
/// </summary>
public sealed class TwilioErrorLoggingHandlerTests
{
    [Fact]
    public async Task ARefusedMessage_LogsTwilioCodeAndMessage_AndLeavesTheBodyReadable()
    {
        // Arrange
        // Live, every send came back 401 and the log said only "not sent", so bad credentials could not be told
        // from a region the account may not text.
        const string Body = """{"code": 20003, "message": "Authenticate", "more_info": "https://www.twilio.com/docs/errors/20003", "status": 401}""";
        var logger = new RecordingLogger();
        using var invoker = CreateInvoker(logger, HttpStatusCode.Unauthorized, Body);

        // Act
        using var response = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Post, "https://api.twilio.com/2010-04-01/Accounts/AC1/Messages.json"), TestContext.Current.CancellationToken);

        // Assert
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("401", entry.Message, StringComparison.Ordinal);
        Assert.Contains("20003", entry.Message, StringComparison.Ordinal);
        Assert.Contains("Authenticate", entry.Message, StringComparison.Ordinal);
        Assert.Equal(Body, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnAcceptedMessage_LogsNothing()
    {
        // Arrange
        var logger = new RecordingLogger();
        using var invoker = CreateInvoker(logger, HttpStatusCode.Created, """{"status": "queued"}""");

        // Act
        using var response = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Post, "https://api.twilio.com/"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(logger.Entries);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    public void AnUnexpectedBody_ReadsAsNoCode(string body)
    {
        // Act
        var (code, message) = TwilioErrorLoggingHandler.ReadError(body);

        // Assert
        Assert.Null(code);
        Assert.Null(message);
    }

    private static HttpMessageInvoker CreateInvoker(ILogger<TwilioErrorLoggingHandler> logger, HttpStatusCode status, string body)
        => new(new TwilioErrorLoggingHandler(logger)
        {
            InnerHandler = new StubHandler(status, body),
        });

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class RecordingLogger : ILogger<TwilioErrorLoggingHandler>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
