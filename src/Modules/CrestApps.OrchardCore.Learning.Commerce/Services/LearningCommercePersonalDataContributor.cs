using CrestApps.OrchardCore.Learning.Commerce.Core.Services;
using CrestApps.OrchardCore.Learning.Commerce.Services;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Learning.Commerce.Services;

/// <summary>
/// Exports the course seats a person holds or was invited to and the learning they bought. Erasing the person gives
/// their seats back to the account and clears them from the seats and the purchases, which stay so the seat ledger and
/// a later refund still find them.
/// </summary>
internal sealed class LearningCommercePersonalDataContributor : IPersonalDataContributor
{
    private const string SeatKind = "LearningSeat";
    private const string PurchaseKind = "LearningPurchase";
    private const string Actor = "Personal data erasure";

    private readonly ILicensePoolService _pools;
    private readonly LicenseSeatStore _seats;
    private readonly LearningPurchaseStore _purchases;

    public LearningCommercePersonalDataContributor(ILicensePoolService pools, LicenseSeatStore seats, LearningPurchaseStore purchases)
    {
        _pools = pools;
        _seats = seats;
        _purchases = purchases;
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        foreach (var seat in await _pools.ListSeatsForPeopleAsync(context.Subject.UserIds, context.Subject.Emails, cancellationToken))
        {
            var record = context.Add(SeatKind, seat.ItemId, seat.PoolId, seat.GivenUtc);

            record.Fields["Pool"] = seat.PoolId;
            record.Fields["Account"] = seat.AccountId;
            record.Fields["Email"] = seat.Email;
            record.Fields["State"] = seat.State.ToString();
        }

        foreach (var purchase in await _purchases.ListByUsersAsync(context.Subject.UserIds, cancellationToken))
        {
            var record = context.Add(PurchaseKind, purchase.ItemId, purchase.ProductId, purchase.GrantedUtc);

            record.Fields["Order"] = purchase.OrderId;
            record.Fields["Line"] = purchase.LineId;
            record.Fields["Product"] = purchase.ProductId;
            record.Fields["Account"] = purchase.AccountId;
            record.Fields["State"] = purchase.State.ToString();
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        foreach (var seat in await _pools.ListSeatsForPeopleAsync(context.Subject.UserIds, context.Subject.Emails, cancellationToken))
        {
            // Taking the seat back releases it on the ledger under the pool's lock and ends the enrollment or the
            // invitation it carried; a seat already taken back is left as it is.
            await _pools.RemoveAsync(seat.ItemId, Actor, cancellationToken);

            // Removing commits, which detaches what was read before: clear the person on a fresh read.
            var fresh = await _seats.FindFreshByIdAsync(seat.ItemId, cancellationToken);

            if (fresh is null)
            {
                continue;
            }

            fresh.UserId = null;
            fresh.Email = null;
            await _seats.UpdateAsync(fresh, cancellationToken);
            context.Result.Count(SeatKind);
        }

        foreach (var purchase in await _purchases.ListByUsersAsync(context.Subject.UserIds, cancellationToken))
        {
            var fresh = await _purchases.FindFreshByIdAsync(purchase.ItemId, cancellationToken);

            if (fresh is null)
            {
                continue;
            }

            fresh.UserId = null;
            await _purchases.UpdateAsync(fresh, cancellationToken);
            context.Result.Count(PurchaseKind);
        }
    }
}
