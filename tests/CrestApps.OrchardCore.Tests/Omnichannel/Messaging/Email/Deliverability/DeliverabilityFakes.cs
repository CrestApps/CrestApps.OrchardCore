using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email.Deliverability;

internal sealed class DeliverabilityClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone) => dateTimeOffset;

    public ITimeZone GetTimeZone(string timeZoneId) => throw new NotSupportedException();

    public ITimeZone GetSystemTimeZone() => throw new NotSupportedException();

    public ITimeZone[] GetTimeZones() => [];
}

internal sealed class InMemoryEmailDeliveryLog : IEmailDeliveryLog
{
    public List<EmailDeliveryLogEntry> Entries { get; } = [];

    public Task RecordAsync(EmailDeliveryLogEntry entry, CancellationToken cancellationToken = default)
    {
        Entries.Add(entry);

        return Task.CompletedTask;
    }

    public void AddSent(string addressId, DateTime occurredUtc, string recipient = "ann@example.com", bool isBulk = true, string messageId = null)
        => Entries.Add(new EmailDeliveryLogEntry
        {
            AddressId = addressId,
            Kind = EmailDeliveryEventKind.Sent,
            Recipient = recipient,
            RecipientDomain = recipient[(recipient.IndexOf('@') + 1)..],
            MessageId = messageId,
            IsBulk = isBulk,
            OccurredUtc = occurredUtc,
        });

    public Task<bool> HasEventAsync(string eventId, CancellationToken cancellationToken = default)
        => Task.FromResult(!string.IsNullOrEmpty(eventId) && Entries.Any(entry => entry.EventId == eventId));

    public Task<int> CountAsync(string addressId, EmailDeliveryEventKind kind, DateTime sinceUtc, string recipientDomain = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Entries.Count(entry =>
            entry.AddressId == addressId &&
            entry.Kind == kind &&
            entry.OccurredUtc >= sinceUtc &&
            (recipientDomain is null || entry.RecipientDomain == recipientDomain)));

    public Task<int> CountForRecipientAsync(string recipient, EmailDeliveryEventKind kind, DateTime sinceUtc, CancellationToken cancellationToken = default)
        => Task.FromResult(Entries.Count(entry => entry.Recipient == recipient && entry.Kind == kind && entry.OccurredUtc >= sinceUtc));

    public Task<DateTime?> GetSentTimeAsync(string addressId, DateTime sinceUtc, int position, string recipientDomain = null, CancellationToken cancellationToken = default)
    {
        var entry = Entries
            .Where(entry => entry.AddressId == addressId && entry.Kind == EmailDeliveryEventKind.Sent && entry.OccurredUtc >= sinceUtc && (recipientDomain is null || entry.RecipientDomain == recipientDomain))
            .OrderBy(entry => entry.OccurredUtc)
            .Skip(position)
            .FirstOrDefault();

        return Task.FromResult(entry?.OccurredUtc);
    }

    public Task<DateTime?> GetLastBulkSentUtcAsync(string addressId, CancellationToken cancellationToken = default)
        => Task.FromResult(Entries
            .Where(entry => entry.AddressId == addressId && entry.Kind == EmailDeliveryEventKind.Sent && entry.IsBulk)
            .Select(entry => (DateTime?)entry.OccurredUtc)
            .Max());

    public Task<string> FindSendingAddressIdAsync(string messageId, string recipient, CancellationToken cancellationToken = default)
    {
        var sent = Entries.Where(entry => entry.Kind == EmailDeliveryEventKind.Sent).ToArray();

        var match = (messageId is null ? null : sent.FirstOrDefault(entry => entry.MessageId == messageId)) ??
            sent.Where(entry => entry.Recipient == recipient).OrderByDescending(entry => entry.OccurredUtc).FirstOrDefault();

        return Task.FromResult(match?.AddressId);
    }

    public Task<int> PruneAsync(DateTime beforeUtc, int maxEntries, CancellationToken cancellationToken = default)
        => Task.FromResult(Entries.RemoveAll(entry => entry.OccurredUtc < beforeUtc));
}

internal sealed class InMemoryEmailSuppressionList : IEmailSuppressionList
{
    public Dictionary<string, EmailSuppression> Items { get; } = new(StringComparer.Ordinal);

    public Task<EmailSuppression> FindAsync(string address, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.GetValueOrDefault(OmnichannelEmailAddress.Normalize(address) ?? string.Empty));

    public Task<bool> SuppressAsync(string address, EmailSuppressionReason reason, string detail, string createdBy = null, CancellationToken cancellationToken = default)
    {
        var key = OmnichannelEmailAddress.Normalize(address);

        if (string.IsNullOrEmpty(key) || Items.ContainsKey(key))
        {
            return Task.FromResult(false);
        }

        Items[key] = new EmailSuppression { Address = key, Reason = reason, Detail = detail, CreatedBy = createdBy };

        return Task.FromResult(true);
    }

    public Task<bool> RemoveAsync(string address, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Remove(OmnichannelEmailAddress.Normalize(address) ?? string.Empty));

    public Task<(IReadOnlyList<EmailSuppression> Items, int Total)> PageAsync(string search, int page, int pageSize, CancellationToken cancellationToken = default)
        => Task.FromResult<(IReadOnlyList<EmailSuppression>, int)>((Items.Values.ToArray(), Items.Count));
}

internal sealed class InMemoryEmailSendingStateStore : IEmailSendingStateStore
{
    public Dictionary<string, EmailSendingState> States { get; } = new(StringComparer.Ordinal);

    public int Saves { get; private set; }

    public Task<EmailSendingState> FindAsync(string addressId, CancellationToken cancellationToken = default)
        => Task.FromResult(addressId is null ? null : States.GetValueOrDefault(addressId));

    public Task SaveAsync(EmailSendingState state, CancellationToken cancellationToken = default)
    {
        States[state.AddressId] = state;
        Saves++;

        return Task.CompletedTask;
    }
}

internal static class DeliverabilityTestFactory
{
    public static EmailSendingGovernor CreateGovernor(
        out InMemoryEmailDeliveryLog log,
        out InMemoryEmailSuppressionList suppressions,
        out InMemoryEmailSendingStateStore states,
        out DeliverabilityClock clock)
    {
        log = new InMemoryEmailDeliveryLog();
        suppressions = new InMemoryEmailSuppressionList();
        states = new InMemoryEmailSendingStateStore();
        clock = new DeliverabilityClock();

        return new EmailSendingGovernor(
            log,
            suppressions,
            states,
            clock,
            NullLogger<EmailSendingGovernor>.Instance,
            new PassThroughStringLocalizer<EmailSendingGovernor>());
    }
}
