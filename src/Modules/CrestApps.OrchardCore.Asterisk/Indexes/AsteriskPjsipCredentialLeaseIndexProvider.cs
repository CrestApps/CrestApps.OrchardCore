using CrestApps.OrchardCore.Asterisk.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Asterisk.Indexes;

/// <summary>
/// Maps <see cref="AsteriskPjsipCredentialLease"/> documents to the <see cref="AsteriskPjsipCredentialLeaseIndex"/>.
/// </summary>
public sealed class AsteriskPjsipCredentialLeaseIndexProvider : IndexProvider<AsteriskPjsipCredentialLease>
{
    /// <inheritdoc/>
    public override void Describe(DescribeContext<AsteriskPjsipCredentialLease> context)
    {
        context
            .For<AsteriskPjsipCredentialLeaseIndex>()
            .Map(lease => new AsteriskPjsipCredentialLeaseIndex
            {
                AuthorizationUser = lease.AuthorizationUser,
                TenantName = lease.TenantName,
                UserId = lease.UserId,
                SessionId = lease.SessionId,
                ExpiresUtc = lease.ExpiresUtc,
                Revoked = lease.RevokedUtc.HasValue,
            });
    }
}
