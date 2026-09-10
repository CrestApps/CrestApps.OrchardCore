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
