namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// A media-quality sample (or an end-of-call summary) measured in the browser from the live WebRTC peer
/// connection and reported to the server for observability and alerting. Values come from
/// <c>RTCPeerConnection.getStats()</c>; the browser computes an estimated Mean Opinion Score (MOS) from loss,
/// jitter, and round-trip time so a single number tracks perceived call quality.
/// </summary>
public sealed class CallQualityReport
{
    /// <summary>
    /// Gets or sets the client call identifier the sample belongs to.
    /// </summary>
    public string CallId { get; set; }

    /// <summary>
    /// Gets or sets the call direction (<c>outbound</c> or <c>inbound</c>).
    /// </summary>
    public string Direction { get; set; }

    /// <summary>
    /// Gets or sets the negotiated audio codec (for example <c>audio/G722</c> or <c>audio/opus</c>).
    /// </summary>
    public string Codec { get; set; }

    /// <summary>
    /// Gets or sets the selected local ICE candidate type (<c>host</c>, <c>srflx</c>, <c>prflx</c>, or
    /// <c>relay</c>). A <c>relay</c> pair means media is flowing through a TURN server.
    /// </summary>
    public string LocalCandidateType { get; set; }

    /// <summary>
    /// Gets or sets the selected remote ICE candidate type.
    /// </summary>
    public string RemoteCandidateType { get; set; }

    /// <summary>
    /// Gets or sets the cumulative inbound audio packets received.
    /// </summary>
    public long PacketsReceived { get; set; }

    /// <summary>
    /// Gets or sets the cumulative inbound audio packets lost.
    /// </summary>
    public long PacketsLost { get; set; }

    /// <summary>
    /// Gets or sets the inbound audio packet loss for the most recent interval, as a percentage.
    /// </summary>
    public double LossPercent { get; set; }

    /// <summary>
    /// Gets or sets the inbound audio jitter in milliseconds.
    /// </summary>
    public double JitterMs { get; set; }

    /// <summary>
    /// Gets or sets the round-trip time of the selected candidate pair in milliseconds.
    /// </summary>
    public double RoundTripTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the cumulative inbound audio bytes received. A sustained zero after the connection is up
    /// indicates broken inbound media (one-way audio).
    /// </summary>
    public long BytesReceived { get; set; }

    /// <summary>
    /// Gets or sets the microphone capture level for this sample (0.0 - 1.0, where 1.0 is full scale), read
    /// from the browser's audio media source. Every other measurement on this report describes the direction
    /// the agent is listening to; this one describes whether the agent can be heard. A sustained value at or
    /// near zero while the call is connected means the caller is hearing silence. It is <c>-1</c> when the
    /// browser reported no capture statistics; check <see cref="CaptureReported"/> before reading it.
    /// </summary>
    public double MicrophoneLevel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the browser reported capture statistics for this sample. When
    /// <see langword="false"/> the microphone level is unknown rather than silent — a distinction worth keeping,
    /// because reporting an absent measurement as zero would send an agent hunting a microphone that works.
    /// </summary>
    public bool CaptureReported { get; set; }

    /// <summary>
    /// Gets or sets the cumulative outbound audio bytes sent.
    /// </summary>
    public long BytesSent { get; set; }

    /// <summary>
    /// Gets or sets the cumulative outbound audio packets sent.
    /// </summary>
    public long PacketsSent { get; set; }

    /// <summary>
    /// Gets or sets the lowest microphone capture level observed over the call (populated on the final
    /// summary). It answers whether the agent was audible for the whole call from the summary alone.
    /// </summary>
    public double MinMicrophoneLevel { get; set; }

    /// <summary>
    /// Gets or sets the average time, in milliseconds, that received audio waited in the browser's jitter
    /// buffer before playout during this sample's window. This is the delay the browser adds on top of the
    /// network round trip, and unlike the round trip nothing about a healthy network keeps it small: a call
    /// can show no loss, low jitter and a good score while the two parties audibly talk over each other. It is
    /// <c>-1</c> when the browser does not report the underlying counters.
    /// </summary>
    public double JitterBufferMs { get; set; }

    /// <summary>
    /// Gets or sets the loudest measured amplitude (0.0 - 1.0 RMS) of the audio arriving from the far end
    /// during this sample's window, measured in the browser because no browser reports it in its statistics.
    /// It is the only measurement that corresponds to "the caller sounds far away", a complaint every other
    /// number on this report is compatible with. It is <c>-1</c> when it could not be measured.
    /// </summary>
    public double InboundLevel { get; set; }

    /// <summary>
    /// Gets or sets the loudest measured amplitude (0.0 - 1.0 RMS) captured from the microphone during this
    /// sample's window. This is measured in the browser rather than read from
    /// <see cref="MicrophoneLevel"/>'s source, so it is available in browsers that do not implement capture
    /// statistics at all. Note it is NOT on the same scale as <see cref="MicrophoneLevel"/> -- conversational
    /// speech peaks around 0.1 - 0.3 here -- which is why the two are kept apart rather than merged. It is
    /// <c>-1</c> when it could not be measured.
    /// </summary>
    public double CaptureProbeLevel { get; set; }

    /// <summary>
    /// Gets or sets the highest jitter-buffer delay observed over the call (populated on the final summary),
    /// so a call that drifted into walkie-talkie territory late is visible without reading every sample.
    /// </summary>
    public double MaxJitterBufferMs { get; set; }

    /// <summary>
    /// Gets or sets the percentage of received audio the browser had to conceal -- invent, because the packet
    /// carrying it arrived too late to play -- during this sample's window. It is the price of a short playout
    /// buffer, so it belongs beside <see cref="JitterBufferMs"/>: a shorter buffer is an improvement only while
    /// this stays near zero, and a few percent is heard as roughness in the far end's voice. It is <c>-1</c>
    /// when the browser does not report the underlying counters.
    /// </summary>
    public double ConcealmentPercent { get; set; }

    /// <summary>
    /// Gets or sets the sample rate, in hertz, the microphone is actually capturing at. It is the first thing
    /// to read on a call that measured well and sounded wrong: a Bluetooth headset can only run its microphone
    /// in hands-free mode, and capture then drops to 8 kHz (narrowband) or 16 kHz (wideband) while the same
    /// headset keeps playing back at full quality -- so the agent hears a flawless call and the far end hears a
    /// thin, hollow, distant voice. No other measurement on this report moves: the codec, the bitrate, the loss
    /// and the score all stay exactly as they were. It is <c>0</c> when the browser reported no capture
    /// settings.
    /// </summary>
    public int CaptureSampleRate { get; set; }

    /// <summary>
    /// Gets or sets the label of the microphone the call is actually using, so a call can be explained without
    /// asking the agent afterwards which device was selected.
    /// </summary>
    public string CaptureDevice { get; set; }

    /// <summary>
    /// Gets or sets the browser audio processing actually applied to the capture, as a compact list of
    /// <c>ec</c> (echo cancellation), <c>ns</c> (noise suppression) and <c>agc</c> (automatic gain control).
    /// These are requested rather than guaranteed, and layering them on top of a headset's own processing is
    /// itself a cause of hollow-sounding outbound audio, so what was applied is worth recording alongside the
    /// format it was applied to.
    /// </summary>
    public string CaptureProcessing { get; set; }

    /// <summary>
    /// Gets or sets the MIME type of the codec the browser is sending (for example <c>audio/opus</c>).
    /// <see cref="Codec"/> is the receive codec; until this was added the direction the far end hears -- the
    /// one every quality complaint has been about -- had no codec on record.
    /// </summary>
    public string SendCodec { get; set; }

    /// <summary>
    /// Gets or sets which statistic <see cref="RoundTripTimeMs"/> came from: <c>candidate-pair</c> (a STUN round
    /// trip over the media path), <c>remote-inbound-rtp</c> (RTCP-derived), or <c>none</c>. A round trip that
    /// looks implausible cannot be judged without knowing which of the two produced it.
    /// </summary>
    public string RttSource { get; set; }

    /// <summary>
    /// Gets or sets the packet loss, in percent, that the provider reports receiving FROM this browser -- the
    /// only far-end-side view of the agent's own audio the browser can see. <c>-1</c> when not reported.
    /// </summary>
    public double RemoteFractionLostPercent { get; set; }

    /// <summary>
    /// Gets or sets the jitter, in milliseconds, that the provider reports receiving from this browser.
    /// <c>-1</c> when not reported.
    /// </summary>
    public double RemoteJitterMs { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the browser reported echo-canceller statistics for the capture.
    /// </summary>
    public bool EchoStatsReported { get; set; }

    /// <summary>
    /// Gets or sets the echo return loss, in decibels, measured by the browser's echo canceller. Meaningful
    /// only when <see cref="EchoStatsReported"/> is <see langword="true"/>.
    /// </summary>
    public double EchoReturnLossDb { get; set; }

    /// <summary>
    /// Gets or sets the echo return loss enhancement, in decibels: how much the echo canceller is removing.
    /// A wired headset has almost no acoustic echo, so a canceller working hard on one is suppressing the
    /// agent's voice -- a classic source of a hollow, distant sound no level measurement can show.
    /// </summary>
    public double EchoReturnLossEnhancementDb { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the browser could see which track the peer connection is sending.
    /// </summary>
    public bool SentTrackReported { get; set; }

    /// <summary>
    /// Gets or sets the label of the track the peer connection is actually sending to the provider.
    /// </summary>
    public string SentTrackLabel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the track being sent is the stream the soft phone captured. The
    /// provider SDK may ignore the stream it is handed on the answer path and capture its own on the browser's
    /// default device with default constraints; when that happens every microphone setting and measurement the
    /// soft phone holds describes a track the far end never hears. This is the direct test of that.
    /// </summary>
    public bool SentTrackIsLocalStream { get; set; }

    /// <summary>
    /// Gets or sets the provider's call-control identifier for this leg, so a browser-side observation can be
    /// joined to the server's webhook and command log for the same leg rather than lined up by timestamp.
    /// </summary>
    public string ProviderCallControlId { get; set; }

    /// <summary>
    /// Gets or sets the provider's leg identifier for this leg.
    /// </summary>
    public string ProviderLegId { get; set; }

    /// <summary>
    /// Gets or sets the provider's session identifier for this call.
    /// </summary>
    public string ProviderSessionId { get; set; }

    /// <summary>
    /// Gets or sets the estimated Mean Opinion Score (1.0 - 4.5) for this sample.
    /// </summary>
    public double Mos { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the browser flagged this sample as a poor connection.
    /// </summary>
    public bool Poor { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the end-of-call summary rather than a periodic sample.
    /// </summary>
    public bool Final { get; set; }

    /// <summary>
    /// Gets or sets the number of samples taken over the call (populated on the final summary).
    /// </summary>
    public int SampleCount { get; set; }

    /// <summary>
    /// Gets or sets the lowest MOS observed over the call (populated on the final summary).
    /// </summary>
    public double MinMos { get; set; }

    /// <summary>
    /// Gets or sets the average MOS over the call (populated on the final summary).
    /// </summary>
    public double AvgMos { get; set; }

    /// <summary>
    /// Gets or sets the highest interval loss percentage observed over the call (populated on the final
    /// summary).
    /// </summary>
    public double MaxLossPercent { get; set; }

    /// <summary>
    /// Gets or sets the measured call duration in milliseconds (populated on the final summary).
    /// </summary>
    public long DurationMs { get; set; }
}
