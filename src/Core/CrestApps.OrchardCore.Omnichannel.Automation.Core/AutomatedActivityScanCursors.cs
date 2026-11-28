namespace CrestApps.OrchardCore.Omnichannel.Automation;

/// <summary>
/// Where the follow-up pass and the owed-reply recovery pass stopped reading the conversations awaiting a customer's
/// answer, so the next pass goes on from there. Each pass reads a bounded number of conversations; starting every pass
/// from the first one would read the same ones over and over and never reach the rest of a large campaign.
/// </summary>
public sealed class AutomatedActivityScanCursors
{
    private long _followUp;
    private long _owedReply;

    /// <summary>
    /// Gets or sets the document id the follow-up pass read up to.
    /// </summary>
    public long FollowUp
    {
        get => Interlocked.Read(ref _followUp);
        set => Interlocked.Exchange(ref _followUp, value);
    }

    /// <summary>
    /// Gets or sets the document id the owed-reply recovery pass read up to.
    /// </summary>
    public long OwedReply
    {
        get => Interlocked.Read(ref _owedReply);
        set => Interlocked.Exchange(ref _owedReply, value);
    }
}
