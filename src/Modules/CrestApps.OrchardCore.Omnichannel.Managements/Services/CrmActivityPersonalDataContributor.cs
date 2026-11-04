using System.Globalization;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PersonalData;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Exports and anonymizes the CRM activities of a person: those of their contacts and leads, and those addressed to their
/// email or phone number. An activity keeps its channel, times, outcome and disposition, which the contact center's
/// reports need; the address it went to is removed, and the person's details are scrubbed from its notes and
/// instructions.
/// </summary>
/// <remarks>
/// It runs before the contacts are anonymized, because it finds the activities through the contacts' emails and phone
/// numbers.
/// </remarks>
internal sealed class CrmActivityPersonalDataContributor : IPersonalDataContributor
{
    public const string ActivityKind = "CrmActivity";

    // A privacy request reads at most this many activities for each way the person is identified.
    private const int MaxActivities = 5_000;

    private readonly CrmPersonalDataContacts _contacts;
    private readonly ISession _session;
    private readonly IOmnichannelActivityStore _activities;

    public CrmActivityPersonalDataContributor(
        CrmPersonalDataContacts contacts,
        ISession session,
        IOmnichannelActivityStore activities)
    {
        _contacts = contacts;
        _session = session;
        _activities = activities;
    }

    public int Order => -10;

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var activity in await ListAsync(context.Subject, cancellationToken))
        {
            var record = context.Add(ActivityKind, activity.ItemId, activity.Channel ?? activity.Kind.ToString(), activity.CreatedUtc);

            record.Fields[nameof(OmnichannelActivity.Kind)] = activity.Kind.ToString();
            record.Fields[nameof(OmnichannelActivity.Channel)] = activity.Channel;
            record.Fields[nameof(OmnichannelActivity.PreferredDestination)] = activity.PreferredDestination;
            record.Fields[nameof(OmnichannelActivity.ScheduledUtc)] = activity.ScheduledUtc.ToString("O", CultureInfo.InvariantCulture);
            record.Fields[nameof(OmnichannelActivity.CompletedUtc)] = activity.CompletedUtc?.ToString("O", CultureInfo.InvariantCulture);
            record.Fields[nameof(OmnichannelActivity.DispositionId)] = activity.DispositionId;
            record.Fields[nameof(OmnichannelActivity.Instructions)] = activity.Instructions;
            record.Fields[nameof(OmnichannelActivity.Notes)] = activity.Notes;
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var activity in await ListAsync(context.Subject, cancellationToken))
        {
            activity.PreferredDestination = null;
            activity.Notes = context.Scrub(activity.Notes);
            activity.Instructions = context.Scrub(activity.Instructions);

            await _activities.UpdateAsync(activity, cancellationToken);
            context.Result.Count(ActivityKind);
        }
    }

    private async Task<IReadOnlyList<OmnichannelActivity>> ListAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, OmnichannelActivity>(StringComparer.Ordinal);
        var contactIds = (await _contacts.FindAsync(subject, cancellationToken)).Select(contact => contact.ContentItemId).ToArray();

        foreach (var chunk in contactIds.Chunk(500))
        {
            foreach (var activity in await _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index => index.ContactContentItemId.IsIn(chunk), collection: OmnichannelConstants.CollectionName)
                .Take(MaxActivities)
                .ListAsync(cancellationToken))
            {
                found.TryAdd(activity.ItemId, activity);
            }
        }

        var destinations = subject.Emails
            .Concat(_contacts.NormalizePhoneNumbers(subject))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (destinations.Length > 0)
        {
            foreach (var activity in await _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index => index.PreferredDestination.IsIn(destinations), collection: OmnichannelConstants.CollectionName)
                .Take(MaxActivities)
                .ListAsync(cancellationToken))
            {
                found.TryAdd(activity.ItemId, activity);
            }
        }

        return [.. found.Values.OrderBy(activity => activity.ScheduledUtc)];
    }
}
