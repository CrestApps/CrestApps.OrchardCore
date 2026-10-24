namespace CrestApps.OrchardCore.Omnichannel.Core;

/// <summary>
/// Thrown by an <see cref="IOmnichannelProcessor"/> whose provider permanently refused the activity's destination
/// (an email hard bounce, for example). No retry can succeed, so the caller closes the activity with
/// <see cref="TerminalReasonCode"/> instead of spending its remaining attempts on an address that will never answer.
/// </summary>
public sealed class OmnichannelActivityRefusedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityRefusedException"/> class.
    /// </summary>
    /// <param name="terminalReasonCode">The terminal reason to close the activity with.</param>
    /// <param name="message">What the provider said.</param>
    public OmnichannelActivityRefusedException(string terminalReasonCode, string message)
        : base(message)
    {
        TerminalReasonCode = terminalReasonCode;
    }

    /// <summary>
    /// Gets the terminal reason to close the activity with, from <see cref="OmnichannelConstants.TerminalReasons"/>.
    /// </summary>
    public string TerminalReasonCode { get; }
}
