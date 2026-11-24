using System.Globalization;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.PersonalData;

/// <summary>
/// Exports and anonymizes a person's time punches. A punch keeps when it was and its kind, which time worked and pay
/// are worked out from; where the person was (coordinates and the device) goes, and their details are scrubbed from the
/// reason given for a correction.
/// </summary>
internal sealed class TimePunchPersonalDataContributor : IPersonalDataContributor
{
    public const string TimePunchKind = "TimePunch";

    // A privacy request reads at most this many punches of each worker.
    private const int MaxPunches = 10_000;

    private readonly IStaffMemberManager _workers;
    private readonly TimePunchStore _punches;

    public TimePunchPersonalDataContributor(
        IStaffMemberManager workers,
        TimePunchStore punches)
    {
        _workers = workers;
        _punches = punches;
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var worker in await _workers.GetByUserIdsAsync(context.Subject.UserIds, cancellationToken))
        {
            foreach (var punch in await _punches.ListForWorkerAsync(worker.ItemId, MaxPunches, cancellationToken))
            {
                var record = context.Add(TimePunchKind, punch.ItemId, punch.Kind.ToString(), punch.CreatedUtc);

                record.Fields[nameof(TimePunch.Kind)] = punch.Kind.ToString();
                record.Fields[nameof(TimePunch.Utc)] = punch.Utc.ToString("O", CultureInfo.InvariantCulture);
                record.Fields[nameof(TimePunch.Source)] = punch.Source.ToString();
                record.Fields[nameof(TimePunch.DeviceId)] = punch.DeviceId;
                record.Fields[nameof(TimePunch.Latitude)] = punch.Latitude?.ToString(CultureInfo.InvariantCulture);
                record.Fields[nameof(TimePunch.Longitude)] = punch.Longitude?.ToString(CultureInfo.InvariantCulture);
                record.Fields[nameof(TimePunch.Job)] = punch.Job;
                record.Fields[nameof(TimePunch.Reason)] = punch.Reason;
            }
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var worker in await _workers.GetByUserIdsAsync(context.Subject.UserIds, cancellationToken))
        {
            foreach (var punch in await _punches.ListForWorkerAsync(worker.ItemId, MaxPunches, cancellationToken))
            {
                if (punch.Latitude is null && punch.Longitude is null && punch.DeviceId is null && !context.Subject.IsMentionedIn(punch.Reason))
                {
                    continue;
                }

                punch.Latitude = null;
                punch.Longitude = null;
                punch.DeviceId = null;
                punch.Reason = context.Scrub(punch.Reason);

                await _punches.UpdateAsync(punch, cancellationToken);
                context.Result.Count(TimePunchKind);
            }
        }
    }
}
