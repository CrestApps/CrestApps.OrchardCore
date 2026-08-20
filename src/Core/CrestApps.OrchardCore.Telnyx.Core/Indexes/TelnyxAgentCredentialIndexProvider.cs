using CrestApps.OrchardCore.Telnyx.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Telnyx.Indexes;

/// <summary>
/// Maps <see cref="TelnyxAgentCredential"/> documents to the <see cref="TelnyxAgentCredentialIndex"/>.
/// </summary>
public sealed class TelnyxAgentCredentialIndexProvider : IndexProvider<TelnyxAgentCredential>
{
    /// <inheritdoc/>
    public override void Describe(DescribeContext<TelnyxAgentCredential> context)
    {
        context
            .For<TelnyxAgentCredentialIndex>()
            .Map(credential => new TelnyxAgentCredentialIndex
            {
                TenantName = credential.TenantName,
                UserId = credential.UserId,
                CredentialId = credential.CredentialId,
                SipUsername = credential.SipUsername,
                ExpiresUtc = credential.ExpiresUtc,
                Revoked = credential.RevokedUtc.HasValue,
            });
    }
}
