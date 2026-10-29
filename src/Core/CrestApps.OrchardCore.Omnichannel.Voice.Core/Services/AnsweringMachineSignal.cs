namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// What the telephony provider has reported about the machine that answered one automated call, as tokens a live
/// session can watch while it holds the line.
/// </summary>
/// <remarks>
/// Tokens rather than flags, for the same reason the end-call and transfer decisions reach the session as tokens:
/// it is busy carrying audio in both directions, and the report arrives from a webhook request on another thread.
/// </remarks>
public sealed class AnsweringMachineSignal
{
    private readonly CancellationTokenSource _machineDetected = new();
    private readonly CancellationTokenSource _greetingEnded = new();
    private readonly CancellationTokenSource _phoneMenuReached = new();

    internal AnsweringMachineSignal(DateTime createdUtc)
    {
        CreatedUtc = createdUtc;
    }

    /// <summary>
    /// Gets a token that is cancelled once the provider has reported that a voicemail or answering machine answered
    /// the call.
    /// </summary>
    public CancellationToken MachineDetected => _machineDetected.Token;

    /// <summary>
    /// Gets a token that is cancelled once the provider has heard the machine's greeting end.
    /// </summary>
    public CancellationToken GreetingEnded => _greetingEnded.Token;

    /// <summary>
    /// Gets a token that is cancelled once the assistant has pressed keys on the call, which makes the machine that
    /// answered a phone menu rather than a voicemail.
    /// </summary>
    public CancellationToken PhoneMenuReached => _phoneMenuReached.Token;

    /// <summary>
    /// Gets when the call's signals were first asked for or reported, so a call nobody forgot is dropped eventually.
    /// </summary>
    internal DateTime CreatedUtc { get; }

    internal void ReportMachineDetected()
        => _machineDetected.Cancel();

    internal void ReportGreetingEnded()
        => _greetingEnded.Cancel();

    internal void ReportPhoneMenuReached()
        => _phoneMenuReached.Cancel();
}
