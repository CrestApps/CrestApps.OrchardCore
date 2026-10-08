using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Purges the messages in queue shared voicemail boxes that were marked as dealt with, measured from that moment. A
/// message still waiting on the team, new or claimed, is never purged, however old it is: it is work nobody has
/// finished. Returning a message to the team clears its resolution time, so a reopened message is live again. The
/// recording is not part of this record; it stays on the interaction and is purged or erased with it.
/// </summary>
public sealed class SharedVoicemailRetentionPolicy : ContactCenterRetentionPolicyBase<SharedVoicemail, SharedVoicemailIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SharedVoicemailRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to find expired records.</param>
    /// <param name="sharedVoicemailStore">The shared voicemail store.</param>
    public SharedVoicemailRetentionPolicy(
        ISession session,
        ISharedVoicemailStore sharedVoicemailStore)
        : base(session, sharedVoicemailStore)
    {
    }

    /// <inheritdoc/>
    public override string EntityName => "SharedVoicemail";

    /// <inheritdoc/>
    protected override bool IsSubjectToLegalHold => true;

    /// <inheritdoc/>
    protected override double GetRetentionDays(ContactCenterRetentionOptions options) => options.SharedVoicemailRetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<SharedVoicemailIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.ResolvedUtc != null
            && index.ResolvedUtc < cutoffUtc
            && index.Status == SharedVoicemailStatus.Resolved;
}
