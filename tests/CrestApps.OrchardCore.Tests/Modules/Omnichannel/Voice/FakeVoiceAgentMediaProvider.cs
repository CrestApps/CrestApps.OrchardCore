using CrestApps.OrchardCore.Telephony.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// A telephony provider that records what the conversation asked it to do instead of carrying real audio. The
/// point of the automated voice split is that the loop can be exercised through a provider like this one; before
/// it, the same coverage needed a Telnyx account and a real phone call.
/// </summary>
internal sealed class FakeVoiceAgentMediaProvider : IVoiceAgentMediaProvider
{
    public FakeVoiceAgentMediaProvider(string technicalName = "Fake")
    {
        TechnicalName = technicalName;
    }

    public string TechnicalName { get; }

    public string DefaultVoice => "fake-default-voice";

    public List<string> Spoken { get; } = [];

    public List<string> Voices { get; } = [];

    public int TranscriptionStarts { get; private set; }

    public int TranscriptionStops { get; private set; }

    public int Hangups { get; private set; }

    public Task<bool> SpeakAsync(string providerCallId, string text, string voice = null, string language = null, CancellationToken cancellationToken = default)
    {
        Spoken.Add(text);
        Voices.Add(voice);

        return Task.FromResult(true);
    }

    public Task<bool> StartTranscriptionAsync(string providerCallId, string language = null, string commandId = null, CancellationToken cancellationToken = default)
    {
        TranscriptionStarts++;

        return Task.FromResult(true);
    }

    public Task<bool> StopTranscriptionAsync(string providerCallId, CancellationToken cancellationToken = default)
    {
        TranscriptionStops++;

        return Task.FromResult(true);
    }

    public Task<bool> GatherAsync(string providerCallId, string text, string validDigits, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<bool> HangupAsync(string providerCallId, CancellationToken cancellationToken = default)
    {
        Hangups++;

        return Task.FromResult(true);
    }
}
