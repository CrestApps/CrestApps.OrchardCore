using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Models;
using CrestApps.OrchardCore.Omnichannel.Services;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// An in-memory contact record, standing in for whatever a host stores contacts in.
/// </summary>
/// <remarks>
/// Implements the read, search and write contracts over one dictionary, so a test that records an
/// opt-out through the writer sees it through the resolver - which is the property the services
/// under test depend on, and the one a mock per contract would quietly fail to give.
/// </remarks>
internal sealed class FakeOmnichannelContactStore : IOmnichannelContactResolver, IOmnichannelContactSearch, IOmnichannelContactWriter
{
    private readonly Dictionary<string, OmnichannelContact> _contacts = new(StringComparer.Ordinal);

    private int _nextId;

    public FakeOmnichannelContactStore(params OmnichannelContact[] contacts)
    {
        foreach (var contact in contacts ?? [])
        {
            _contacts[contact.Id] = contact;
        }
    }

    /// <summary>
    /// Gets or sets the time an opt-out is stamped with.
    /// </summary>
    public DateTime UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Gets how many change sets were applied.
    /// </summary>
    public int AppliedCount { get; private set; }

    /// <summary>
    /// Adds a contact, and returns it so a test can keep hold of it.
    /// </summary>
    /// <param name="contact">The contact.</param>
    /// <returns>The contact.</returns>
    public OmnichannelContact Add(OmnichannelContact contact)
    {
        contact.Id ??= $"contact-{++_nextId}";
        _contacts[contact.Id] = contact;

        return contact;
    }

    public Task<OmnichannelContact> FindByIdAsync(string contactId, CancellationToken cancellationToken = default)
        => Task.FromResult(contactId is not null && _contacts.TryGetValue(contactId, out var contact) ? contact : null);

    public Task<IReadOnlyCollection<OmnichannelContact>> FindByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<OmnichannelContact>>(
            [.. _contacts.Values.Where(contact => contact.PhoneNumbers.Any(number =>
                string.Equals(number.Number, phoneNumber, StringComparison.Ordinal)))]);

    public Task<IReadOnlyCollection<OmnichannelContact>> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<OmnichannelContact>>(
            [.. _contacts.Values.Where(contact => contact.Emails.Any(entry =>
                string.Equals(entry.Email, email, StringComparison.OrdinalIgnoreCase)))]);

    public Task<PageResult<OmnichannelContact>> SearchAsync(ContactSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var matches = _contacts.Values.AsEnumerable();

        if (query.DefinitionNames.Count > 0)
        {
            matches = matches.Where(contact => query.DefinitionNames.Contains(contact.DefinitionName, StringComparer.Ordinal));
        }

        if (query.DoNotCall is bool doNotCall)
        {
            matches = matches.Where(contact => contact.DoNotCall == doNotCall);
        }

        if (query.DoNotSms is bool doNotSms)
        {
            matches = matches.Where(contact => contact.DoNotSms == doNotSms);
        }

        if (query.DoNotEmail is bool doNotEmail)
        {
            matches = matches.Where(contact => contact.DoNotEmail == doNotEmail);
        }

        var all = matches.ToArray();

        return Task.FromResult(new PageResult<OmnichannelContact>
        {
            Count = all.Length,
            Entries = [.. all.Skip(Math.Max(0, query.Page - 1) * query.PageSize).Take(query.PageSize)],
        });
    }

    public Task<OmnichannelContact> CreateAsync(OmnichannelContact contact, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contact);

        return Task.FromResult(Add(contact));
    }

    public Task<bool> ApplyAsync(string contactId, OmnichannelContactChanges changes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changes);

        if (contactId is null || !_contacts.TryGetValue(contactId, out var contact))
        {
            return Task.FromResult(false);
        }

        var changed = false;

        if (changes.FirstName is not null && !string.Equals(contact.FirstName, changes.FirstName, StringComparison.Ordinal))
        {
            contact.FirstName = changes.FirstName;
            changed = true;
        }

        if (changes.LastName is not null && !string.Equals(contact.LastName, changes.LastName, StringComparison.Ordinal))
        {
            contact.LastName = changes.LastName;
            changed = true;
        }

        if (changes.TimeZoneId is not null && !string.Equals(contact.TimeZoneId, changes.TimeZoneId, StringComparison.Ordinal))
        {
            contact.TimeZoneId = changes.TimeZoneId;
            changed = true;
        }

        if (changes.DoNotCall is bool wantsDoNotCall && contact.DoNotCall != wantsDoNotCall)
        {
            contact.SetDoNotCall(wantsDoNotCall, UtcNow);
            changed = true;
        }

        if (changes.DoNotSms is bool wantsDoNotSms && contact.DoNotSms != wantsDoNotSms)
        {
            contact.SetDoNotSms(wantsDoNotSms, UtcNow);
            changed = true;
        }

        if (changes.DoNotEmail is bool wantsDoNotEmail && contact.DoNotEmail != wantsDoNotEmail)
        {
            contact.SetDoNotEmail(wantsDoNotEmail, UtcNow);
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(changes.Email) &&
            !string.Equals(contact.GetPrimaryEmail(), changes.Email, StringComparison.OrdinalIgnoreCase))
        {
            contact.Emails.Clear();
            contact.Emails.Add(new ContactEmail { Email = changes.Email.Trim() });
            changed = true;
        }

        if (changes.PhoneNumber is not null && !string.IsNullOrWhiteSpace(changes.PhoneNumber.Number))
        {
            var existing = contact.PhoneNumbers.FirstOrDefault(number =>
                string.Equals(number.Type, changes.PhoneNumber.Type, StringComparison.OrdinalIgnoreCase));

            if (existing is null || !string.Equals(existing.Number, changes.PhoneNumber.Number, StringComparison.Ordinal))
            {
                if (existing is not null)
                {
                    contact.PhoneNumbers.Remove(existing);
                }

                contact.PhoneNumbers.Add(changes.PhoneNumber);
                changed = true;
            }
        }

        if (changed)
        {
            AppliedCount++;
        }

        return Task.FromResult(changed);
    }
}
