using CrestApps.OrchardCore.Core.Throttling;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.OrchardCore.AI.Chat.Core.Services;

/// <summary>
/// Gives <see cref="ConnectionAddressAIVisitorIdentityResolver"/> the visitor's address on every chat hub call. On the
/// long-polling transport a hub method runs after the request that opened the connection has ended, so the request
/// accessor has no request and the visitor had no address: the session-start limiter then had no key and let every
/// start through. SignalR keeps the connection's own copy of that request, and its address is taken from there.
/// </summary>
public sealed class ChatHubConnectionAddressFilter : IHubFilter
{
    private static readonly AsyncLocal<string> _currentAddress = new();

    /// <summary>
    /// Gets the network of the visitor whose hub call is running, or <see langword="null"/> outside one.
    /// </summary>
    public static string CurrentAddress
        => _currentAddress.Value;

    /// <inheritdoc/>
    public async ValueTask<object> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object>> next)
    {
        ArgumentNullException.ThrowIfNull(invocationContext);
        ArgumentNullException.ThrowIfNull(next);

        var previous = _currentAddress.Value;
        _currentAddress.Value = invocationContext.Context?.GetHttpContext() is { } httpContext
            ? ClientNetworkKey.NetworkOnly(httpContext)
            : null;

        try
        {
            return await next(invocationContext);
        }
        finally
        {
            _currentAddress.Value = previous;
        }
    }
}
