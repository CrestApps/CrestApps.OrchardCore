using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Models;

/// <summary>
/// Kept on an AI call's activity while the caller's keypad is private for a secure entry. Every node reads the
/// activity, so a keypad event that lands on a node with no copy of the entry (another node's memory cache, a restart,
/// an evicted or unreadable entry) is still dropped rather than handed on as the caller's turn.
/// </summary>
internal sealed class SecureCaptureWindow
{
    /// <summary>
    /// Gets or sets what the caller is asked to type.
    /// </summary>
    public SecureCaptureField Field { get; set; }

    /// <summary>
    /// Gets or sets when the window closes on its own, if the entry is never finished.
    /// </summary>
    public DateTime UntilUtc { get; set; }
}
