using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Fundraising.Models;

namespace CrestApps.OrchardCore.Fundraising.Services;

/// <summary>
/// Exports a donor's donations, found by their account, and anonymizes them: the donor's name, email and any mention of
/// them in a tribute are replaced. The amounts and receipt numbers stay, because a donation is a financial record.
/// </summary>
public sealed class DonationPersonalDataContributor : ICommercePersonalDataContributor
{
    /// <summary>
    /// The kind of record donations are exported as.
    /// </summary>
    public const string RecordKind = "Donation";

    /// <inheritdoc/>
    /// <remarks>
    /// The guest retention expiry never reached these records, so only an explicit erasure request does.
    /// </remarks>
    public bool TakesPartInRetentionExpiry => false;

    private readonly IDonationStore _donations;

    /// <summary>
    /// Initializes a new instance of the <see cref="DonationPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="donations">The store of donations.</param>
    public DonationPersonalDataContributor(IDonationStore donations)
    {
        _donations = donations;
    }

    /// <inheritdoc/>
    public async Task IdentifyAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        foreach (var donation in await FindAsync(subject, cancellationToken))
        {
            subject.AddEmail(donation.DonorEmail);
            subject.AddName(donation.DonorName);
        }
    }

    /// <inheritdoc/>
    public async Task ExportAsync(CommercePersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var donation in await FindAsync(context.Subject, cancellationToken))
        {
            context.Export.Records.Add(new CommercePersonalDataRecord
            {
                Kind = RecordKind,
                Id = donation.ItemId,
                CreatedUtc = donation.CreatedUtc,
                Values =
                {
                    [nameof(Donation.CampaignTitle)] = donation.CampaignTitle,
                    [nameof(Donation.OrderNumber)] = donation.OrderNumber,
                    [nameof(Donation.DonorName)] = donation.DonorName,
                    [nameof(Donation.DonorEmail)] = donation.DonorEmail,
                    [nameof(Donation.Tribute)] = donation.Tribute,
                    [nameof(Donation.ReceiptNumber)] = donation.ReceiptNumber,
                },
            });
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(CommercePersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var donation in await FindAsync(context.Subject, cancellationToken))
        {
            donation.DonorName = context.ReplaceName(donation.DonorName);
            donation.DonorEmail = null;
            donation.Tribute = context.Scrub(donation.Tribute);

            await _donations.UpdateAsync(donation, cancellationToken);
            context.Result.Count(RecordKind);
        }
    }

    private async Task<IReadOnlyList<Donation>> FindAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, Donation>(StringComparer.Ordinal);

        foreach (var userId in subject.UserIds)
        {
            foreach (var donation in await _donations.ListByDonorAsync(userId, cancellationToken))
            {
                found[donation.ItemId] = donation;
            }
        }

        return [.. found.Values];
    }
}
