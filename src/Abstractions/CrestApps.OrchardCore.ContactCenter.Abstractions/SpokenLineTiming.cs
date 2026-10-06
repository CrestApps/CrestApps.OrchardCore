namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Estimates how long a transcribed line took to say. A call's transcript keeps one time per line, so where a line
/// ended has to be estimated from what was said: the transcript of a recorded call uses it to measure the silence
/// between lines, and a voice agent uses it to place a caller's line, which is only transcribed once they have
/// finished it, at about the moment they started.
/// </summary>
public static class SpokenLineTiming
{
    /// <summary>
    /// The speaking rate the estimate assumes, in words per second. About 156 words a minute: the middle of
    /// conversational English, and close to the pace of the neural voices the voice agents speak with.
    /// </summary>
    public const double WordsPerSecond = 2.6;

    /// <summary>
    /// The shortest a line is taken to last, so a one-word "yes" still occupies a moment of the call.
    /// </summary>
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromSeconds(0.6);

    /// <summary>
    /// Estimates how long it took to say a line.
    /// </summary>
    /// <param name="text">The words said.</param>
    /// <returns>The estimated length of the line; <see cref="TimeSpan.Zero"/> when there are no words.</returns>
    public static TimeSpan EstimateDuration(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return TimeSpan.Zero;
        }

        var words = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;
        var estimate = TimeSpan.FromSeconds(words / WordsPerSecond);

        return estimate < MinimumDuration ? MinimumDuration : estimate;
    }
}
