using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A configurable call-control authorization boundary that records what it was asked to authorize.
/// </summary>
/// <remarks>
/// Call control is a fail-closed boundary, so a test that cannot observe whether the boundary was consulted
/// cannot tell an authorized command apart from a command that skipped authorization entirely. Recording the
/// contexts makes that difference assertable.
/// </remarks>
public sealed class FakeCallControlAuthorizationService : ICallControlAuthorizationService
{
    private readonly Func<CallControlAuthorizationContext, CallControlAuthorizationResult> _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeCallControlAuthorizationService"/> class.
    /// </summary>
    /// <param name="handler">The authorization decision to apply, or <see langword="null"/> to authorize every request.</param>
    public FakeCallControlAuthorizationService(
        Func<CallControlAuthorizationContext, CallControlAuthorizationResult> handler = null)
    {
        _handler = handler ?? (context => new CallControlAuthorizationResult
        {
            Succeeded = true,
            AgentId = context.UserId,
            ProviderCallId = context.ProviderCallId,
        });
    }

    /// <summary>
    /// Gets every context the boundary was asked to authorize, in call order.
    /// </summary>
    public List<CallControlAuthorizationContext> Contexts { get; } = [];

    /// <summary>
    /// Creates a boundary that denies every request.
    /// </summary>
    /// <returns>The denying boundary.</returns>
    public static FakeCallControlAuthorizationService Denying()
        => new(_ => CallControlAuthorizationResult.Denied());

    /// <summary>
    /// Creates a boundary that authorizes every request and resolves the supplied provider call identifier.
    /// </summary>
    /// <param name="providerCallId">The server-resolved provider call identifier to return.</param>
    /// <returns>The authorizing boundary.</returns>
    public static FakeCallControlAuthorizationService Resolving(string providerCallId)
        => new(context => new CallControlAuthorizationResult
        {
            Succeeded = true,
            AgentId = context.UserId,
            ProviderCallId = providerCallId,
        });

    /// <inheritdoc/>
    public Task<CallControlAuthorizationResult> AuthorizeAsync(
        CallControlAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        Contexts.Add(context);

        return Task.FromResult(_handler(context));
    }
}
