using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Identifies a telephony provider and the capabilities it advertises. Executable operations live on the
/// separate capability contracts a provider chooses to implement, so a provider is never obliged to answer
/// for an operation it cannot perform.
/// </summary>
public interface ITelephonyProvider
{
    /// <summary>
    /// Gets the localized, human-readable name of the provider.
    /// </summary>
    LocalizedString Name { get; }

    /// <summary>
    /// Gets the set of operations the provider supports. Advertising a capability is not sufficient on its
    /// own: the provider must also implement the matching executable contract or the operation fails closed.
    /// </summary>
    TelephonyCapabilities Capabilities { get; }
}
