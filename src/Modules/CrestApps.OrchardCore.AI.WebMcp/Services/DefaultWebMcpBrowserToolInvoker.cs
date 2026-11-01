using System.Collections.Concurrent;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Runs page tools in a user's browser tab and keeps the waiting calls in memory. The tab's SignalR connection and the
/// chat completion must therefore be served by the same server.
/// </summary>
public sealed class DefaultWebMcpBrowserToolInvoker : IWebMcpBrowserToolInvoker
{
    private readonly ConcurrentDictionary<string, PendingCall> _pendingCalls = new(StringComparer.Ordinal);
    private readonly IWebMcpBrowserClient _client;
    private readonly WebMcpChatOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultWebMcpBrowserToolInvoker"/> class.
    /// </summary>
    /// <param name="client">The client that reaches the browser tabs.</param>
    /// <param name="options">The chat bridge options.</param>
    /// <param name="logger">The logger.</param>
    public DefaultWebMcpBrowserToolInvoker(
        IWebMcpBrowserClient client,
        IOptions<WebMcpChatOptions> options,
        ILogger<DefaultWebMcpBrowserToolInvoker> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<WebMcpBrowserToolResult> InvokeAsync(string connectionId, string toolName, string argumentsJson, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionId);
        ArgumentException.ThrowIfNullOrEmpty(toolName);

        var callId = Guid.NewGuid().ToString("N");
        var call = new PendingCall(connectionId, new TaskCompletionSource<WebMcpBrowserToolResult>(TaskCreationOptions.RunContinuationsAsynchronously));

        _pendingCalls[callId] = call;

        try
        {
            try
            {
                await _client.SendExecuteToolAsync(connectionId, callId, toolName, string.IsNullOrEmpty(argumentsJson) ? "{}" : argumentsJson, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "The page tool '{ToolName}' could not be sent to the browser.", toolName);

                return WebMcpBrowserToolResult.Failure("The page that offers this tool could not be reached.");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            timeout.CancelAfter(_options.ToolCallTimeout);

            try
            {
                return await call.Completion.Task.WaitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return WebMcpBrowserToolResult.Failure("The page did not answer in time.");
            }
        }
        finally
        {
            _pendingCalls.TryRemove(callId, out _);
        }
    }

    /// <inheritdoc/>
    public bool TryComplete(string callId, string connectionId, string text, bool isError)
    {
        if (string.IsNullOrEmpty(callId) ||
            !_pendingCalls.TryGetValue(callId, out var call) ||
            !string.Equals(call.ConnectionId, connectionId, StringComparison.Ordinal))
        {
            return false;
        }

        var result = isError
            ? WebMcpBrowserToolResult.Failure(text)
            : WebMcpBrowserToolResult.Success(text);

        return call.Completion.TrySetResult(result);
    }

    /// <inheritdoc/>
    public void CancelConnection(string connectionId)
    {
        foreach (var entry in _pendingCalls)
        {
            if (string.Equals(entry.Value.ConnectionId, connectionId, StringComparison.Ordinal))
            {
                entry.Value.Completion.TrySetResult(WebMcpBrowserToolResult.Failure("The page was closed before the tool answered."));
            }
        }
    }

    private sealed record PendingCall(string ConnectionId, TaskCompletionSource<WebMcpBrowserToolResult> Completion);
}
