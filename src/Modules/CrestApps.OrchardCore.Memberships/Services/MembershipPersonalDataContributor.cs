using System.Globalization;
using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Memberships.Models;

namespace CrestApps.OrchardCore.Memberships.Services;

/// <summary>
/// Exports a member's memberships, found by their account, and anonymizes the user name each one copied. The
/// membership itself stays, because it records what was bought and the roles it gave.
/// </summary>
public sealed class MembershipPersonalDataContributor : ICommercePersonalDataContributor
{
    /// <summary>
    /// The kind of record memberships are exported as.
    /// </summary>
    public const string RecordKind = "Membership";

    /// <inheritdoc/>
    /// <remarks>
    /// The guest retention expiry never reached these records, so only an explicit erasure request does.
    /// </remarks>
    public bool TakesPartInRetentionExpiry => false;

    private readonly IMembershipStore _memberships;

    /// <summary>
    /// Initializes a new instance of the <see cref="MembershipPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="memberships">The store of memberships.</param>
    public MembershipPersonalDataContributor(IMembershipStore memberships)
    {
        _memberships = memberships;
    }

    /// <inheritdoc/>
    public Task IdentifyAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task ExportAsync(CommercePersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var membership in await FindAsync(context.Subject, cancellationToken))
        {
            context.Export.Records.Add(new CommercePersonalDataRecord
            {
                Kind = RecordKind,
                Id = membership.ItemId,
                CreatedUtc = membership.CreatedUtc,
                Values =
                {
                    [nameof(Membership.Title)] = membership.Title,
                    [nameof(Membership.UserName)] = membership.UserName,
                    [nameof(Membership.Status)] = membership.Status.ToString(),
                    [nameof(Membership.StartsUtc)] = membership.StartsUtc.ToString("O", CultureInfo.InvariantCulture),
                    [nameof(Membership.EndsUtc)] = membership.EndsUtc?.ToString("O", CultureInfo.InvariantCulture),
                    [nameof(Membership.RoleNames)] = string.Join(", ", membership.RoleNames ?? []),
                },
            });
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(CommercePersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var membership in await FindAsync(context.Subject, cancellationToken))
        {
            membership.UserName = context.ReplaceName(membership.UserName);
            membership.EndedReason = context.Scrub(membership.EndedReason);

            await _memberships.UpdateAsync(membership, cancellationToken);
            context.Result.Count(RecordKind);
        }
    }

    private async Task<IReadOnlyList<Membership>> FindAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, Membership>(StringComparer.Ordinal);

        foreach (var userId in subject.UserIds)
        {
            foreach (var membership in await _memberships.ListByUserAsync(userId, cancellationToken))
            {
                found[membership.ItemId] = membership;
            }
        }

        return [.. found.Values];
    }
}
