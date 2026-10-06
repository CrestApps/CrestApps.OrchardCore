namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What a dialer profile's recent calls measured, for sizing how many calls predictive dialing places: how often a
/// person answers, how often an answered call is abandoned, and how long calls take to answer, to connect and to handle.
/// </summary>
/// <remarks>
/// <para>
/// The counts cover every call the profile placed in the window. The answer rate and the timings are measured from the
/// most recent calls only (the sample), so measuring a busy campaign stays cheap.
/// </para>
/// <para>
/// A call that is still ringing has no outcome yet, so it is left out of the answer rate: counting it as unanswered would
/// make the rate look lower than it is, and a lower rate places more calls.
/// </para>
/// </remarks>
public sealed class DialerPacingStatistics
{
    /// <summary>
    /// Gets or sets the start of the window measured.
    /// </summary>
    public DateTime FromUtc { get; set; }

    /// <summary>
    /// Gets or sets the end of the window measured.
    /// </summary>
    public DateTime ToUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of calls the profile placed in the window.
    /// </summary>
    public long Attempts { get; set; }

    /// <summary>
    /// Gets or sets the number of calls a person, not a machine, answered in the window: the denominator of the
    /// abandonment rate, counted exactly as the abandonment cap counts it.
    /// </summary>
    public long LiveAnswers { get; set; }

    /// <summary>
    /// Gets or sets the number of answered calls abandoned in the window: the numerator of the abandonment rate, counted
    /// exactly as the abandonment cap counts it.
    /// </summary>
    public long AbandonedCalls { get; set; }

    /// <summary>
    /// Gets or sets the number of sampled calls that have an outcome: a person answered, or the call ended.
    /// </summary>
    public long SettledAttempts { get; set; }

    /// <summary>
    /// Gets or sets the number of sampled calls a person answered.
    /// </summary>
    public long SettledLiveAnswers { get; set; }

    /// <summary>
    /// Gets or sets the median time from placing a call to learning a person answered it, or <see langword="null"/> when
    /// no sampled call was answered.
    /// </summary>
    public TimeSpan? MedianRingToAnswer { get; set; }

    /// <summary>
    /// Gets or sets the 75th percentile of the time from placing a call to learning a person answered it.
    /// </summary>
    public TimeSpan? P75RingToAnswer { get; set; }

    /// <summary>
    /// Gets or sets the number of sampled calls the ring-to-answer times are measured from.
    /// </summary>
    public int RingToAnswerSamples { get; set; }

    /// <summary>
    /// Gets or sets the median time from a person answering to an agent being connected to them.
    /// </summary>
    public TimeSpan? MedianConnectLatency { get; set; }

    /// <summary>
    /// Gets or sets the 95th percentile of the time from a person answering to an agent being connected to them.
    /// </summary>
    public TimeSpan? P95ConnectLatency { get; set; }

    /// <summary>
    /// Gets or sets the number of sampled calls the connect times are measured from.
    /// </summary>
    public int ConnectLatencySamples { get; set; }

    /// <summary>
    /// Gets or sets the average time an agent spent on a call, from being connected to the call ending.
    /// </summary>
    public TimeSpan? AverageTalkTime { get; set; }

    /// <summary>
    /// Gets or sets the number of sampled calls the talk time is measured from.
    /// </summary>
    public int TalkTimeSamples { get; set; }

    /// <summary>
    /// Gets or sets the average time an agent spent wrapping a call up.
    /// </summary>
    public TimeSpan? AverageWrapUpTime { get; set; }

    /// <summary>
    /// Gets or sets the number of sampled calls the wrap-up time is measured from.
    /// </summary>
    public int WrapUpTimeSamples { get; set; }

    /// <summary>
    /// Gets the share of settled sampled calls a person answered, from 0 to 1, or <see langword="null"/> when no sampled
    /// call has settled.
    /// </summary>
    public double? AnswerRate => SettledAttempts == 0
        ? null
        : (double)SettledLiveAnswers / SettledAttempts;

    /// <summary>
    /// Gets the abandonment rate in percent of calls a person answered, or <see langword="null"/> when nobody answered.
    /// </summary>
    public double? AbandonmentRatePercent => LiveAnswers == 0
        ? null
        : (double)AbandonedCalls / LiveAnswers * 100;
}
