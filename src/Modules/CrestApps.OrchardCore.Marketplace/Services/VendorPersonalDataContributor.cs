using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Marketplace.Models;

namespace CrestApps.OrchardCore.Marketplace.Services;

/// <summary>
/// Exports the vendors a person runs, found by their account, and takes the person off them when their data is
/// anonymized: their user and the user name the vendor copied are removed. The vendor stays, since it is a business with
/// its own sales and payouts.
/// </summary>
public sealed class VendorPersonalDataContributor : ICommercePersonalDataContributor
{
    /// <summary>
    /// The kind of record vendor memberships are exported as.
    /// </summary>
    public const string RecordKind = "Vendor";

    /// <inheritdoc/>
    /// <remarks>
    /// The guest retention expiry never reached these records, so only an explicit erasure request does.
    /// </remarks>
    public bool TakesPartInRetentionExpiry => false;

    private readonly IVendorStore _vendors;

    /// <summary>
    /// Initializes a new instance of the <see cref="VendorPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="vendors">The store of vendors.</param>
    public VendorPersonalDataContributor(IVendorStore vendors)
    {
        _vendors = vendors;
    }

    /// <inheritdoc/>
    public Task IdentifyAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task ExportAsync(CommercePersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var (vendor, userId) in await FindAsync(context.Subject, cancellationToken))
        {
            var index = vendor.UserIds.IndexOf(userId);

            context.Export.Records.Add(new CommercePersonalDataRecord
            {
                Kind = RecordKind,
                Id = vendor.ItemId,
                Values =
                {
                    [nameof(Vendor.Name)] = vendor.Name,
                    [nameof(Vendor.Email)] = vendor.Email,
                    ["UserName"] = index >= 0 && index < vendor.UserNames.Count ? vendor.UserNames[index] : null,
                },
            });
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(CommercePersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var (vendor, userId) in await FindAsync(context.Subject, cancellationToken))
        {
            var index = vendor.UserIds.IndexOf(userId);

            if (index < 0)
            {
                continue;
            }

            vendor.UserIds.RemoveAt(index);

            if (index < vendor.UserNames.Count)
            {
                vendor.UserNames.RemoveAt(index);
            }

            // A vendor's own address that is the person's is theirs too.
            if (context.Subject.IsEmail(vendor.Email))
            {
                vendor.Email = null;
            }

            await _vendors.UpdateAsync(vendor, cancellationToken);
            context.Result.Count(RecordKind);
        }
    }

    private async Task<IReadOnlyList<(Vendor Vendor, string UserId)>> FindAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var found = new List<(Vendor, string)>();

        foreach (var userId in subject.UserIds)
        {
            foreach (var vendor in await _vendors.ListByUserAsync(userId, cancellationToken))
            {
                vendor.UserIds ??= [];
                vendor.UserNames ??= [];
                found.Add((vendor, userId));
            }
        }

        return found;
    }
}
