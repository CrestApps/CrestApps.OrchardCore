namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Carries the state an <see cref="IIncomingCallContextProvider"/> uses to contribute cards for a
/// ringing inbound call. Providers add cards to <see cref="Cards"/> and may share state through
/// <see cref="Items"/>.
/// </summary>
public sealed class IncomingCallContributionContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IncomingCallContributionContext"/> class.
    /// </summary>
    /// <param name="call">The ringing inbound call.</param>
    /// <param name="userId">The identifier of the user the call is being offered to.</param>
    public IncomingCallContributionContext(TelephonyCall call, string userId)
    {
        Call = call;
        UserId = userId;
    }

    /// <summary>
    /// Gets the ringing inbound call the cards are contributed for.
    /// </summary>
    public TelephonyCall Call { get; }

    /// <summary>
    /// Gets the identifier of the user the call is being offered to.
    /// </summary>
    public string UserId { get; }

    /// <summary>
    /// Gets or sets the optional heading shown above the contributed cards.
    /// </summary>
    public string Heading { get; set; }

    /// <summary>
    /// Gets the cards contributed so far. Providers add their cards to this collection.
    /// </summary>
    public IList<IncomingCallCard> Cards { get; } = [];

    /// <summary>
    /// Gets the metadata contributors attach for the client, such as a queue name or offer lifecycle URLs.
    /// </summary>
    public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets a mutable bag providers can use to share state while contributing cards.
    /// </summary>
    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();
}
