using CrestApps.Core.Sms;

namespace CrestApps.OrchardCore.Core.Sms;

/// <summary>
/// Implemented by an Orchard Core SMS provider that is only a shim over a framework provider, so the
/// resolver can hand back the framework provider instead of wrapping the shim again.
/// </summary>
/// <remarks>
/// Without this, resolving a framework provider that was registered with Orchard through a shim
/// returns shim-wrapped-in-adapter. Both layers satisfy the plain provider contract, so everything
/// still compiles and every send still succeeds - but the capabilities the inner provider carries
/// beyond that contract, such as reporting its own message identifier, are no longer visible to the
/// caller. Delivery receipts then stop matching the message they belong to, silently.
/// </remarks>
public interface ICoreSmsProviderAdapter
{
    /// <summary>
    /// Gets the framework provider this shim delegates to.
    /// </summary>
    ISmsProvider InnerProvider { get; }
}
