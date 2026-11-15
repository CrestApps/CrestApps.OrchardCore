using CrestApps.OrchardCore.Learning.Commerce.Core.Services;
using CrestApps.OrchardCore.Learning.Commerce.Services;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Learning.Commerce.Services;

/// <summary>
/// Exports the course seats a person holds or was invited to, and deletes them when the person is erased; the account
/// keeps its pools.
/// </summary>
internal sealed class LearningCommercePersonalDataContributor : IPersonalDataContributor
{
    private const string Kind = "LearningSeat";

    private readonly ILicensePoolService _pools;
    private readonly LicenseSeatStore _seats;

    public LearningCommercePersonalDataContributor(ILicensePoolService pools, LicenseSeatStore seats)
    {
        _pools = pools;
        _seats = seats;
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        foreach (var seat in await _pools.ListSeatsForPeopleAsync(context.Subject.UserIds, context.Subject.Emails, cancellationToken))
        {
            var record = context.Add(Kind, seat.ItemId, seat.PoolId, seat.GivenUtc);

            record.Fields["Pool"] = seat.PoolId;
            record.Fields["Account"] = seat.AccountId;
            record.Fields["Email"] = seat.Email;
            record.Fields["State"] = seat.State.ToString();
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        foreach (var seat in await _pools.ListSeatsForPeopleAsync(context.Subject.UserIds, context.Subject.Emails, cancellationToken))
        {
            await _seats.DeleteAsync(seat, cancellationToken);
            context.Result.Count(Kind);
        }
    }
}
