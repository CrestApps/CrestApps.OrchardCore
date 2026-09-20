namespace CrestApps.Core.ContactCenter;

/// <summary>
/// One check the Contact Center runs once, when a tenant starts, to report a misconfiguration an operator can
/// act on.
/// </summary>
/// <remarks>
/// <para>
/// These used to be Orchard <c>IModularTenantEvents</c>, which meant the reasoning lived somewhere only Orchard
/// could run it. The verdict is framework work; when it runs is the host's business. A host with a tenant
/// lifecycle calls these on activation, and a host without one runs them from a hosted service at start.
/// </para>
/// <para>
/// A check reports rather than throws. Failing a tenant's activation over a configuration mistake takes away
/// the administration screens - the one place an operator would go to correct it - so a check records its
/// verdict somewhere a health check can surface it and lets the tenant come up.
/// </para>
/// </remarks>
public interface IContactCenterStartupCheck
{
    /// <summary>
    /// Gets the name this check is reported under.
    /// </summary>
    /// <remarks>
    /// Used in diagnostics when a check fails, so it should name the thing being checked rather than the class
    /// doing the checking.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Runs the check.
    /// </summary>
    /// <remarks>
    /// Implementations do not throw to report a bad configuration. An exception here means the check itself
    /// could not run, which is a different thing and is worth surfacing as one.
    /// </remarks>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ValidateAsync(CancellationToken cancellationToken = default);
}
