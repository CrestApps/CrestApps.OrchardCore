using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Providers.CustomLlm;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Api;

public sealed class CustomLlmChatClientTests
{
    [Fact]
    public async Task StreamsTheServersReply_AndSendsTheConversation()
    {
        // Arrange
        string received = null;
        var client = new CustomLlmChatClient(Connect(async server =>
        {
            received = await ReceiveTextAsync(server);
            await SendTextAsync(server, """{"type":"delta","content":"Your order "}""");
            await SendTextAsync(server, """{"type":"delta","content":"ships today."}""");
            await SendTextAsync(server, """{"type":"done","usage":{"inputTokens":12,"outputTokens":4}}""");
        }), "my-agent");

        // Act
        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.System, "Be brief."), new ChatMessage(ChatRole.User, "Where is order 42?")],
            new ChatOptions { ConversationId = "s-1" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Your order ships today.", response.Text);
        Assert.Equal(16, response.Usage?.TotalTokenCount);

        using var request = JsonDocument.Parse(received);
        Assert.Equal("request", request.RootElement.GetProperty("type").GetString());
        Assert.Equal("my-agent", request.RootElement.GetProperty("model").GetString());
        Assert.Equal("s-1", request.RootElement.GetProperty("conversationId").GetString());
        Assert.Equal("user", request.RootElement.GetProperty("messages")[1].GetProperty("role").GetString());
        Assert.Equal("Where is order 42?", request.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task AnErrorFromTheServer_Throws()
    {
        // Arrange
        var client = new CustomLlmChatClient(Connect(async server =>
        {
            await ReceiveTextAsync(server);
            await SendTextAsync(server, """{"type":"error","message":"model overloaded"}""");
        }), "my-agent");

        // Act & Assert
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hi")], cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("model overloaded", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AServerThatGoesQuiet_TimesOut()
    {
        // Arrange
        var client = new CustomLlmChatClient(Connect(async server =>
        {
            await ReceiveTextAsync(server);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }), "my-agent")
        {
            ReplyTimeout = TimeSpan.FromMilliseconds(300),
        };

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(() => client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hi")], cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("https://llm.example.com/agent", "wss://llm.example.com/agent")]
    [InlineData("http://localhost:8080/ws", "ws://localhost:8080/ws")]
    [InlineData("wss://llm.example.com/agent", "wss://llm.example.com/agent")]
    public void ToWebSocketUri_TurnsHttpIntoItsWebSocketForm(string endpoint, string expected)
        => Assert.Equal(expected, CustomLlmClientProvider.ToWebSocketUri(new Uri(endpoint)).AbsoluteUri);

    // A WebSocket pair over a loopback TCP connection: the client side for the chat client, the server side for the test.
    private static Func<CancellationToken, Task<WebSocket>> Connect(Func<WebSocket, Task> serve)
        => async cancellationToken =>
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, cancellationToken);
            var serverSide = await listener.AcceptTcpClientAsync(cancellationToken);
            listener.Stop();

            var server = WebSocket.CreateFromStream(serverSide.GetStream(), isServer: true, subProtocol: null, keepAliveInterval: Timeout.InfiniteTimeSpan);
            _ = Task.Run(async () =>
            {
                using (serverSide)
                using (server)
                {
                    await serve(server);
                }
            }, CancellationToken.None);

            return WebSocket.CreateFromStream(tcpClient.GetStream(), isServer: false, subProtocol: null, keepAliveInterval: Timeout.InfiniteTimeSpan);
        };

    private static async Task<string> ReceiveTextAsync(WebSocket socket)
    {
        var buffer = new byte[16384];
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);

        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    private static Task SendTextAsync(WebSocket socket, string text)
        => socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);
}
