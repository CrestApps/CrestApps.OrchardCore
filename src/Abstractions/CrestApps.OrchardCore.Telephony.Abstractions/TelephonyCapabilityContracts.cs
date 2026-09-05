using System.Collections.Frozen;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Maps each advertised telephony capability to the executable contract a provider must implement before the
/// capability may be exercised.
/// </summary>
public static class TelephonyCapabilityContracts
{
    private static readonly FrozenDictionary<TelephonyCapabilities, Type> _contractsByCapability = new Dictionary<TelephonyCapabilities, Type>
    {
        [TelephonyCapabilities.Dial] = typeof(ITelephonyCallControlProvider),
        [TelephonyCapabilities.Hangup] = typeof(ITelephonyCallControlProvider),
        [TelephonyCapabilities.Hold] = typeof(ITelephonyHoldProvider),
        [TelephonyCapabilities.Resume] = typeof(ITelephonyHoldProvider),
        [TelephonyCapabilities.Mute] = typeof(ITelephonyMuteProvider),
        [TelephonyCapabilities.Transfer] = typeof(ITelephonyTransferProvider),
        [TelephonyCapabilities.AttendedTransfer] = typeof(ITelephonyAttendedTransferProvider),
        [TelephonyCapabilities.Merge] = typeof(ITelephonyConferenceProvider),
        [TelephonyCapabilities.SendDigits] = typeof(ITelephonyDtmfProvider),
        [TelephonyCapabilities.ReceiveCalls] = typeof(ITelephonyInboundCallProvider),
        [TelephonyCapabilities.Voicemail] = typeof(ITelephonyVoicemailProvider),
        [TelephonyCapabilities.Directory] = typeof(ITelephonyDirectoryProvider),
        [TelephonyCapabilities.ExtensionDial] = typeof(ITelephonyExtensionDialProvider),
        [TelephonyCapabilities.ExtensionConference] = typeof(ITelephonyExtensionDialProvider),
    }.ToFrozenDictionary();

    /// <summary>
    /// Gets the executable contract required by each advertised capability.
    /// </summary>
    public static IReadOnlyDictionary<TelephonyCapabilities, Type> ContractsByCapability => _contractsByCapability;

    /// <summary>
    /// Gets the executable contract required by the given capability.
    /// </summary>
    /// <param name="capability">The advertised capability.</param>
    /// <returns>The contract type, or <see langword="null"/> when the capability requires none.</returns>
    public static Type GetContract(TelephonyCapabilities capability)
        => _contractsByCapability.TryGetValue(capability, out var contract) ? contract : null;
}
