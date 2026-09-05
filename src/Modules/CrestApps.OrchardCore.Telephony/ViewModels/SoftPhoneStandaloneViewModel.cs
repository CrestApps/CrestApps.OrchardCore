using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Telephony.ViewModels;

/// <summary>
/// The model for the standalone <c>/softphone</c> page hosted by the CrestApps Soft Phone browser extension.
/// </summary>
public sealed class SoftPhoneStandaloneViewModel
{
    /// <summary>
    /// Gets the built soft phone widget shape rendered full-window on the page.
    /// </summary>
    public IShape Shape { get; init; }

    /// <summary>
    /// Gets the optional call id the page should auto-answer on load. Set by the extension when the agent
    /// answers an inbound call from the OS notification while the phone window was closed. The client answers
    /// it only when it matches the current pending inbound offer.
    /// </summary>
    public string AnswerCallId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the page is embedded by the extension. When <see langword="true"/> the
    /// page renders the phone expanded to fill the window and suppresses the floating/close chrome.
    /// </summary>
    public bool Embedded { get; init; }
}
