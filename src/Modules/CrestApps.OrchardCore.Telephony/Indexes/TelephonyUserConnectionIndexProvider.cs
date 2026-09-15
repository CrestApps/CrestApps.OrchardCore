using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Entities;
using OrchardCore.Users.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Telephony.Indexes;

/// <summary>
/// Maps stored telephony user-connection metadata from Orchard users into
/// <see cref="TelephonyUserConnectionIndex"/> rows.
/// </summary>
public sealed class TelephonyUserConnectionIndexProvider : IndexProvider<User>
{
    private readonly ILookupNormalizer _lookupNormalizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyUserConnectionIndexProvider"/> class.
    /// </summary>
    /// <param name="lookupNormalizer">The lookup normalizer used for email lookups.</param>
    public TelephonyUserConnectionIndexProvider(ILookupNormalizer lookupNormalizer)
    {
        _lookupNormalizer = lookupNormalizer;
    }

    public override void Describe(DescribeContext<User> context)
    {
        context.For<TelephonyUserConnectionIndex>()
            .Map(user =>
            {
                var rows = new List<TelephonyUserConnectionIndex>();

                if (user is not IEntity entity ||
                    !entity.TryGet<TelephonyUserConnections>(out var connections) ||
                    connections.Connections is null)
                {
                    return rows;
                }

                foreach (var (providerName, tokens) in connections.Connections)
                {
                    if (string.IsNullOrWhiteSpace(providerName) || tokens is null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(tokens.RemoteUserId) &&
                        string.IsNullOrWhiteSpace(tokens.RemoteUserEmail) &&
                        string.IsNullOrWhiteSpace(tokens.RemotePhoneNumber))
                    {
                        continue;
                    }

                    rows.Add(new TelephonyUserConnectionIndex
                    {
                        ProviderName = TrimToLength(providerName, 128),
                        UserId = user.UserId,
                        RemoteUserId = TrimToLength(tokens.RemoteUserId, 64),
                        NormalizedRemoteUserEmail = NormalizeEmail(tokens.RemoteUserEmail),
                        NormalizedRemotePhoneNumber = TrimToLength(
                            TelephonyAddressNormalizer.NormalizePhoneNumber(tokens.RemotePhoneNumber),
                            64),
                        IsEnabled = user.IsEnabled,
                    });
                }

                return rows;
            });
    }

    private string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return TrimToLength(_lookupNormalizer.NormalizeEmail(email.Trim()), 255);
    }

    private static string TrimToLength(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= maxLength ? trimmed : trimmed.Substring(0, maxLength);
    }
}
