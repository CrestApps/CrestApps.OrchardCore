using System.Security.Claims;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.OrchardCore.AI.WebMcp.Hubs;

/// <summary>
/// The hub the chat bridge script connects to: a tab reports its page tools here, and answers the page tool calls the
/// AI chat makes.
/// </summary>
[Authorize]
public sealed class WebMcpHub : Hub
{
    private readonly IWebMcpBrowserToolRegistry _registry;
    private readonly IWebMcpBrowserToolInvoker _invoker;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebMcpHub"/> class.
    /// </summary>
    /// <param name="registry">The registry of the page tools each user's tab offers.</param>
    /// <param name="invoker">The invoker that waits for page tool answers.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public WebMcpHub(
        IWebMcpBrowserToolRegistry registry,
        IWebMcpBrowserToolInvoker invoker,
        IAuthorizationService authorizationService)
    {
        _registry = registry;
        _invoker = invoker;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Records the page tools this tab offers, in place of what the user's tabs reported before. Tools that fail the
    /// checks are dropped.
    /// </summary>
    /// <param name="tools">The tools the tab reports.</param>
    /// <returns>How many tools were recorded.</returns>
    public async Task<int> RegisterPageTools(WebMcpBrowserToolDescriptor[] tools)
    {
        var user = Context.User;
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId) ||
            !await _authorizationService.AuthorizeAsync(user, WebMcpPermissionsProvider.UseWebMcpTools))
        {
            throw new HubException("You may not give page tools to the AI chat.");
        }

        var validTools = WebMcpBrowserToolValidator.Validate(tools);

        if (validTools.Count == 0)
        {
            _registry.Remove(userId, Context.ConnectionId);

            return 0;
        }

        _registry.Register(userId, Context.ConnectionId, validTools);

        return validTools.Count;
    }

    /// <summary>
    /// Forgets the page tools this tab reported.
    /// </summary>
    public void ClearPageTools()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrEmpty(userId))
        {
            _registry.Remove(userId, Context.ConnectionId);
        }
    }

    /// <summary>
    /// Records this tab's answer to a page tool call.
    /// </summary>
    /// <param name="callId">The identifier of the call.</param>
    /// <param name="result">The text the tool answered.</param>
    /// <param name="isError">Whether the answer reports a failure.</param>
    /// <returns><see langword="true"/> when the answer completed a call sent to this tab; otherwise <see langword="false"/>.</returns>
    public bool SubmitToolResult(string callId, string result, bool isError)
    {
        return _invoker.TryComplete(callId, Context.ConnectionId, result, isError);
    }

    /// <summary>
    /// Forgets the tab's page tools and ends the calls waiting on it.
    /// </summary>
    /// <param name="exception">The error that closed the connection, if any.</param>
    /// <returns>A task that completes when the connection is cleaned up.</returns>
    public override Task OnDisconnectedAsync(Exception exception)
    {
        _registry.RemoveConnection(Context.ConnectionId);
        _invoker.CancelConnection(Context.ConnectionId);

        return base.OnDisconnectedAsync(exception);
    }
}
