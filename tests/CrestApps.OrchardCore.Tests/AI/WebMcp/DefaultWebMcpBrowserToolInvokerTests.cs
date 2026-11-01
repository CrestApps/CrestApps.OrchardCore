using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

public sealed class DefaultWebMcpBrowserToolInvokerTests
{
    [Fact]
    public async Task InvokeAsync_AsksTheTabToRunTheToolAndAnswersWhatItSends()
    {
        // Arrange
        var client = new Mock<IWebMcpBrowserClient>();
        DefaultWebMcpBrowserToolInvoker invoker = null;

        client
            .Setup(c => c.SendExecuteToolAsync("connection-1", It.IsAny<string>(), "page_get_info", """{"a":1}""", It.IsAny<CancellationToken>()))
            .Callback((string connectionId, string callId, string _, string _, CancellationToken _) =>
                Task.Run(() => invoker.TryComplete(callId, connectionId, "The page.", false)))
            .Returns(Task.CompletedTask);

        invoker = CreateInvoker(client.Object);

        // Act
        var result = await invoker.InvokeAsync("connection-1", "page_get_info", """{"a":1}""", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal("The page.", result.Text);
    }

    [Fact]
    public async Task InvokeAsync_PassesOnAnErrorTheTabReports()
    {
        // Arrange
        var client = new Mock<IWebMcpBrowserClient>();
        DefaultWebMcpBrowserToolInvoker invoker = null;

        client
            .Setup(c => c.SendExecuteToolAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string connectionId, string callId, string _, string _, CancellationToken _) =>
                Task.Run(() => invoker.TryComplete(callId, connectionId, "No such field.", true)))
            .Returns(Task.CompletedTask);

        invoker = CreateInvoker(client.Object);

        // Act
        var result = await invoker.InvokeAsync("connection-1", "page_set_form_field", "{}", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("No such field.", result.Text);
    }

    [Fact]
    public async Task TryComplete_RefusesAnAnswerFromAnotherConnection()
    {
        // Arrange
        var client = new Mock<IWebMcpBrowserClient>();
        var completions = new TaskCompletionSource<bool[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        DefaultWebMcpBrowserToolInvoker invoker = null;

        client
            .Setup(c => c.SendExecuteToolAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string connectionId, string callId, string _, string _, CancellationToken _) =>
                Task.Run(() =>
                {
                    var forged = invoker.TryComplete(callId, "connection-2", "Forged.", false);
                    var real = invoker.TryComplete(callId, connectionId, "Real.", false);

                    completions.SetResult([forged, real]);
                }))
            .Returns(Task.CompletedTask);

        invoker = CreateInvoker(client.Object);

        // Act
        var result = await invoker.InvokeAsync("connection-1", "page_get_info", "{}", TestContext.Current.CancellationToken);
        var answers = await completions.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Real.", result.Text);
        Assert.Equal([false, true], answers);
    }

    [Fact]
    public async Task InvokeAsync_GivesUpWhenTheTabDoesNotAnswerInTime()
    {
        // Arrange
        var client = new Mock<IWebMcpBrowserClient>();

        client
            .Setup(c => c.SendExecuteToolAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var invoker = CreateInvoker(client.Object, TimeSpan.FromMilliseconds(50));

        // Act
        var result = await invoker.InvokeAsync("connection-1", "page_get_info", "{}", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("in time", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelConnection_EndsTheCallsWaitingOnAClosedTab()
    {
        // Arrange
        var client = new Mock<IWebMcpBrowserClient>();
        DefaultWebMcpBrowserToolInvoker invoker = null;

        client
            .Setup(c => c.SendExecuteToolAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string connectionId, string _, string _, string _, CancellationToken _) =>
                Task.Run(() => invoker.CancelConnection(connectionId)))
            .Returns(Task.CompletedTask);

        invoker = CreateInvoker(client.Object);

        // Act
        var result = await invoker.InvokeAsync("connection-1", "page_get_info", "{}", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("closed", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_ReportsATabItCannotReach()
    {
        // Arrange
        var client = new Mock<IWebMcpBrowserClient>();

        client
            .Setup(c => c.SendExecuteToolAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Gone."));

        var invoker = CreateInvoker(client.Object);

        // Act
        var result = await invoker.InvokeAsync("connection-1", "page_get_info", "{}", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsError);
        Assert.DoesNotContain("Gone.", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_StopsWhenTheCallerCancels()
    {
        // Arrange
        var client = new Mock<IWebMcpBrowserClient>();

        client
            .Setup(c => c.SendExecuteToolAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var invoker = CreateInvoker(client.Object);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invoker.InvokeAsync("connection-1", "page_get_info", "{}", cancellation.Token));
    }

    [Fact]
    public void TryComplete_RefusesAnUnknownCall()
    {
        // Arrange
        var invoker = CreateInvoker(Mock.Of<IWebMcpBrowserClient>());

        // Act & Assert
        Assert.False(invoker.TryComplete("unknown", "connection-1", "x", false));
        Assert.False(invoker.TryComplete(null, "connection-1", "x", false));
    }

    private static DefaultWebMcpBrowserToolInvoker CreateInvoker(IWebMcpBrowserClient client, TimeSpan? timeout = null)
    {
        var options = new WebMcpChatOptions
        {
            ToolCallTimeout = timeout ?? TimeSpan.FromSeconds(10),
        };

        return new DefaultWebMcpBrowserToolInvoker(
            client,
            Options.Create(options),
            NullLogger<DefaultWebMcpBrowserToolInvoker>.Instance);
    }
}
