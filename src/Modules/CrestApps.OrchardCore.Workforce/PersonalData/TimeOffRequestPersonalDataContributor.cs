using System.Globalization;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.PersonalData;

/// <summary>
/// Exports and anonymizes a person's time-off requests. A request keeps its dates, type, amount and decision, which
/// balances and pay need; the reason the person gave (which may be about their health or family) is removed, and their
/// details are scrubbed from the manager's note.
/// </summary>
internal sealed class TimeOffRequestPersonalDataContributor : IPersonalDataContributor
{
    public const string TimeOffRequestKind = "TimeOffRequest";

    // A privacy request reads at most this many requests of each worker.
    private const int MaxRequests = 5_000;

    private readonly IStaffMemberManager _workers;
    private readonly TimeOffRequestStore _requests;

    public TimeOffRequestPersonalDataContributor(
        IStaffMemberManager workers,
        TimeOffRequestStore requests)
    {
        _workers = workers;
        _requests = requests;
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var request in await ListAsync(context.Subject, cancellationToken))
        {
            var title = $"{request.StartDate.ToString("O", CultureInfo.InvariantCulture)} - {request.EndDate.ToString("O", CultureInfo.InvariantCulture)}";
            var record = context.Add(TimeOffRequestKind, request.ItemId, title, request.CreatedUtc);

            record.Fields[nameof(TimeOffRequest.TypeId)] = request.TypeId;
            record.Fields[nameof(TimeOffRequest.StartDate)] = request.StartDate.ToString("O", CultureInfo.InvariantCulture);
            record.Fields[nameof(TimeOffRequest.EndDate)] = request.EndDate.ToString("O", CultureInfo.InvariantCulture);
            record.Fields[nameof(TimeOffRequest.Amount)] = request.Amount.ToString(CultureInfo.InvariantCulture);
            record.Fields[nameof(TimeOffRequest.Status)] = request.Status.ToString();
            record.Fields[nameof(TimeOffRequest.Reason)] = request.Reason;
            record.Fields[nameof(TimeOffRequest.DecisionNote)] = request.DecisionNote;
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var request in await ListAsync(context.Subject, cancellationToken))
        {
            if (string.IsNullOrEmpty(request.Reason) && !context.Subject.IsMentionedIn(request.DecisionNote))
            {
                continue;
            }

            request.Reason = null;
            request.DecisionNote = context.Scrub(request.DecisionNote);

            await _requests.UpdateAsync(request, cancellationToken);
            context.Result.Count(TimeOffRequestKind);
        }
    }

    private async Task<IReadOnlyList<TimeOffRequest>> ListAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var requests = new List<TimeOffRequest>();

        foreach (var worker in await _workers.GetByUserIdsAsync(subject.UserIds, cancellationToken))
        {
            requests.AddRange(await _requests.ListForWorkerAsync(worker.ItemId, MaxRequests, cancellationToken));
        }

        return requests;
    }
}
