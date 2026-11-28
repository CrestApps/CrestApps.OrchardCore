using System.Globalization;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Privacy;
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
/// email or phone number that belong to no other contact. An activity keeps its channel, times, outcome and disposition, which the contact center's
/// reports need; the address it went to is removed, and the person's details are scrubbed from its notes and
/// instructions. The AI conversation an automated activity held with the person (an SMS or email exchange) is exported
/// with it, and erasing deletes it, with what the AI features keep about it, through the AI conversation privacy
/// service.
/// </summary>
/// <remarks>
/// It runs before the contacts are anonymized, because it finds the activities through the contacts' emails and phone
/// numbers.
/// </remarks>
internal sealed class CrmActivityPersonalDataContributor : IPersonalDataContributor
{
    public const string ActivityKind = "CrmActivity";

    public const string AIMessageKind = "CrmActivityAIMessage";

    public const string AISessionKind = "CrmActivityAISession";

    // A privacy request reads at most this many activities for each way the person is identified.
    private const int MaxActivities = 5_000;

    private readonly CrmPersonalDataContacts _contacts;
    private readonly ISession _session;
    private readonly IOmnichannelActivityStore _activities;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IAIChatSessionManager _chatSessions;
    private readonly AIConversationPrivacyService _conversationPrivacy;

    public CrmActivityPersonalDataContributor(
        CrmPersonalDataContacts contacts,
        ISession session,
        IOmnichannelActivityStore activities,
        IEnumerable<IAIChatSessionPromptStore> promptStores,
        IEnumerable<IAIChatSessionManager> chatSessionManagers,
        IEnumerable<AIConversationPrivacyService> conversationPrivacy)
    {
        _contacts = contacts;
        _session = session;
        _activities = activities;

        // The AI chat session services are registered by the AI features, which may be off; the last registration is
        // the one the container would resolve.
        _promptStore = promptStores?.LastOrDefault();
        _chatSessions = chatSessionManagers?.LastOrDefault();
        _conversationPrivacy = conversationPrivacy?.LastOrDefault();
    }

    public int Order => -10;

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var activities = await ListAsync(context.Subject, cancellationToken);

        foreach (var activity in activities)
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

        foreach (var (sessionId, activityId) in GetAISessions(activities))
        {
            await ExportAISessionAsync(context, sessionId, activityId, cancellationToken);
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var activities = await ListAsync(context.Subject, cancellationToken);

        foreach (var activity in activities)
        {
            activity.PreferredDestination = null;
            activity.Notes = context.Scrub(activity.Notes);
            activity.Instructions = context.Scrub(activity.Instructions);

            await _activities.UpdateAsync(activity, cancellationToken);
            context.Result.Count(ActivityKind);
        }

        foreach (var (sessionId, _) in GetAISessions(activities))
        {
            await AnonymizeAISessionAsync(context, sessionId, cancellationToken);
        }
    }

    internal async Task ExportAISessionAsync(PersonalDataExportContext context, string sessionId, string activityId, CancellationToken cancellationToken)
    {
        if (_chatSessions is not null && await _chatSessions.FindByIdAsync(sessionId, cancellationToken) is { } chatSession)
        {
            var record = context.Add(AISessionKind, sessionId, chatSession.Title, chatSession.CreatedUtc);

            record.Fields["ActivityId"] = activityId;
            record.Fields[nameof(chatSession.Title)] = chatSession.Title;

            if (chatSession.ExtractedData is { } extractedData)
            {
                foreach (var field in extractedData)
                {
                    record.Fields[$"{nameof(chatSession.ExtractedData)}.{field.Key}"] = field.Value?.Values is { } values
                        ? string.Join(", ", values)
                        : null;
                }
            }
        }

        if (_promptStore is null)
        {
            return;
        }

        var prompts = (await _promptStore.GetPromptsAsync(sessionId))?.ToList() ?? [];

        foreach (var prompt in prompts)
        {
            var role = prompt.Role.ToString();
            var record = context.Add(AIMessageKind, prompt.ItemId, role, prompt.CreatedUtc);

            record.Fields["SessionId"] = sessionId;
            record.Fields["ActivityId"] = activityId;
            record.Fields[nameof(prompt.Role)] = role;
            record.Fields[nameof(prompt.Content)] = prompt.Content;
        }
    }

    private async Task AnonymizeAISessionAsync(PersonalDataAnonymizeContext context, string sessionId, CancellationToken cancellationToken)
    {
        // The conversation is what the person wrote and what was said to them, so it is deleted rather than scrubbed.
        // The AI conversation privacy service deletes it with its transcript and runs its privacy handlers, so what the
        // AI features keep about it (the data extracted from it, its evaluations) goes too. The activity keeps its
        // reference, which then finds nothing.
        if (_conversationPrivacy is not null && await _conversationPrivacy.DeleteAsync(sessionId, cancellationToken))
        {
            context.Result.Count(AISessionKind);
        }
    }

    private async Task<IReadOnlyList<OmnichannelActivity>> ListAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, OmnichannelActivity>(StringComparer.Ordinal);
        var match = await _contacts.MatchAsync(subject, cancellationToken);
        var contactIds = match.Contacts.Select(contact => contact.ContentItemId).ToArray();
        var personsContacts = new HashSet<string>(contactIds, StringComparer.Ordinal);

        foreach (var chunk in contactIds.Chunk(500))
        {
            foreach (var activity in await _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index => index.ContactContentItemId.IsIn(chunk), collection: OmnichannelConstants.CollectionName)
                .Take(MaxActivities)
                .ListAsync(cancellationToken))
            {
                found.TryAdd(activity.ItemId, activity);
            }
        }

        // A number several contacts share is nobody's in particular, so it finds no activity.
        var destinations = GetDestinations(
            subject.Emails,
            _contacts.NormalizePhoneNumbers(subject).Except(match.SharedPhoneNumbers, StringComparer.Ordinal));

        if (destinations.Length > 0)
        {
            foreach (var activity in await _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index => index.PreferredDestination.IsIn(destinations), collection: OmnichannelConstants.CollectionName)
                .Take(MaxActivities)
                .ListAsync(cancellationToken))
            {
                if (IsPersonsActivity(activity, personsContacts))
                {
                    found.TryAdd(activity.ItemId, activity);
                }
            }
        }

        return [.. found.Values.OrderBy(activity => activity.ScheduledUtc)];
    }

    /// <summary>
    /// Returns the destinations an activity of the person may have been addressed to: each email as it was given and in
    /// its canonical form, and each phone number.
    /// </summary>
    /// <remarks>
    /// An email activity's destination is stored in the canonical lower-case form (see
    /// <see cref="OmnichannelEmailAddress"/>), and PostgreSQL and SQLite compare text by case, so an email given as
    /// <c>Ann@Example.com</c> must also be looked for as <c>ann@example.com</c>. The values are kept apart by exact
    /// spelling, so the canonical form is not dropped as a duplicate of the one given.
    /// </remarks>
    /// <param name="emails">The person's emails.</param>
    /// <param name="phoneNumbers">The person's phone numbers in E.164 form.</param>
    internal static string[] GetDestinations(IEnumerable<string> emails, IEnumerable<string> phoneNumbers)
        => OmnichannelEmailAddress.WithCanonicalForms(emails)
            .Concat(phoneNumbers ?? [])
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Returns whether an activity addressed to one of the person's emails or numbers is the person's: an activity of
    /// another contact belongs to that contact, even when it went to the same inbox or number.
    /// </summary>
    internal static bool IsPersonsActivity(OmnichannelActivity activity, IReadOnlySet<string> personsContacts)
        => string.IsNullOrEmpty(activity.ContactContentItemId) || personsContacts.Contains(activity.ContactContentItemId);

    // Each AI conversation once, with the first of the person's activities that held it.
    private static IEnumerable<(string SessionId, string ActivityId)> GetAISessions(IEnumerable<OmnichannelActivity> activities)
        => activities
            .Where(activity => !string.IsNullOrWhiteSpace(activity.AISessionId))
            .DistinctBy(activity => activity.AISessionId, StringComparer.Ordinal)
            .Select(activity => (activity.AISessionId, activity.ItemId));
}
