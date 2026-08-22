using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Telephony.Sms.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Telephony.Sms.Services;

/// <summary>
/// Resolves the Omnichannel contact for a customer phone number by matching the normalized (E.164) primary
/// cell or home number on the <see cref="OmnichannelContactIndex"/>, so a conversation links to the CRM
/// contact and the portal shows who the agent is talking to.
/// </summary>
public sealed class SmsContactResolver : ISmsContactResolver
{
    private readonly ISession _session;

    public SmsContactResolver(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public async ValueTask<string> ResolveContactContentItemIdAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return null;
        }

        var normalized = phoneNumber.GetCleanedPhoneNumber();

        var match = await _session.QueryIndex<OmnichannelContactIndex>(
                index => index.Published &&
                    (index.NormalizedPrimaryCellPhoneNumber == normalized || index.NormalizedPrimaryHomePhoneNumber == normalized))
            .FirstOrDefaultAsync(cancellationToken);

        return match?.ContentItemId;
    }
}
