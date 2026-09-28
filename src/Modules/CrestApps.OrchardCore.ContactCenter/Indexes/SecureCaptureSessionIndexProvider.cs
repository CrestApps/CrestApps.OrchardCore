using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="SecureCaptureSession"/> documents to the <see cref="SecureCaptureSessionIndex"/>.
/// </summary>
public sealed class SecureCaptureSessionIndexProvider : IndexProvider<SecureCaptureSession>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SecureCaptureSessionIndexProvider"/> class.
    /// </summary>
    public SecureCaptureSessionIndexProvider()
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<SecureCaptureSession> context)
    {
        context
            .For<SecureCaptureSessionIndex>()
            .Map(session => new SecureCaptureSessionIndex
            {
                ItemId = session.ItemId,
                InteractionId = session.InteractionId,
                AgentId = session.AgentId,
                State = session.State,
                EngagedRecordingPause = session.EngagedRecordingPause,
                RecordingResumed = session.RecordingResumed,
                AccessTokenHash = session.AccessTokenHash,
                ExpiresUtc = session.ExpiresUtc,
                ModifiedUtc = session.ModifiedUtc,
            });
    }
}
