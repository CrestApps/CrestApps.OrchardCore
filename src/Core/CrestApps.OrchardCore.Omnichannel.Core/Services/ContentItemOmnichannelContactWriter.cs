using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Models;
using CrestApps.OrchardCore.Omnichannel.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Writes to Orchard Core contact content items.
/// </summary>
public sealed class ContentItemOmnichannelContactWriter : IOmnichannelContactWriter
{
    private readonly IContentManager _contentManager;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentItemOmnichannelContactWriter"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="phoneNumberService">The phone number service used to split a number into the parts the field stores.</param>
    /// <param name="timeProvider">The time provider used to stamp an opt-out.</param>
    public ContentItemOmnichannelContactWriter(
        IContentManager contentManager,
        IPhoneNumberService phoneNumberService,
        TimeProvider timeProvider)
    {
        _contentManager = contentManager;
        _phoneNumberService = phoneNumberService;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task<OmnichannelContact> CreateAsync(OmnichannelContact contact, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentException.ThrowIfNullOrWhiteSpace(contact.DefinitionName);

        var contentItem = await _contentManager.NewAsync(contact.DefinitionName);
        contentItem.DisplayText = contact.DisplayText;

        contentItem.Alter<OmnichannelContactInfoPart>(part =>
        {
            part.FirstName = new TextField { Text = contact.FirstName };
            part.LastName = new TextField { Text = contact.LastName };
        });

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        contentItem.Alter<OmnichannelContactPart>(part =>
        {
            part.TimeZoneId = contact.TimeZoneId;
            part.SetDoNotCall(contact.DoNotCall, utcNow);
            part.SetDoNotSms(contact.DoNotSms, utcNow);
            part.SetDoNotEmail(contact.DoNotEmail, utcNow);
        });

        foreach (var email in contact.Emails ?? [])
        {
            await UpsertEmailAsync(contentItem, email?.Email);
        }

        foreach (var phoneNumber in contact.PhoneNumbers ?? [])
        {
            await UpsertPhoneNumberAsync(contentItem, phoneNumber);
        }

        await _contentManager.CreateAsync(contentItem);

        return ContentItemOmnichannelContactProjection.Project(contentItem);
    }

    /// <inheritdoc/>
    public async Task<bool> ApplyAsync(string contactId, OmnichannelContactChanges changes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changes);

        if (string.IsNullOrEmpty(contactId))
        {
            return false;
        }

        var contentItem = await _contentManager.GetAsync(contactId, VersionOptions.Latest);

        if (contentItem is null)
        {
            return false;
        }

        var changed = ApplyNames(contentItem, changes);
        changed |= ApplyPreferences(contentItem, changes);
        changed |= await UpsertEmailAsync(contentItem, changes.Email);
        changed |= await UpsertPhoneNumberAsync(contentItem, changes.PhoneNumber);

        // Committed only when something actually differs, so a conversation that learns nothing new
        // does not produce a version of the contact that says the same thing.
        if (!changed)
        {
            return false;
        }

        await _contentManager.UpdateAsync(contentItem);

        // Updating writes the draft. The lists that decide who gets dialled or messaged query the
        // published record, so without this a customer could ask not to be called, be recorded exactly
        // right, and be dialled again on the next load.
        if (contentItem.Published)
        {
            await _contentManager.PublishAsync(contentItem);
        }

        return true;
    }

    /// <summary>
    /// Applies the name and time zone.
    /// </summary>
    /// <param name="contentItem">The contact content item.</param>
    /// <param name="changes">The changes.</param>
    /// <returns><see langword="true"/> when something changed.</returns>
    private static bool ApplyNames(ContentItem contentItem, OmnichannelContactChanges changes)
    {
        var changed = false;

        if (changes.FirstName is not null || changes.LastName is not null)
        {
            contentItem.Alter<OmnichannelContactInfoPart>(part =>
            {
                if (changes.FirstName is not null && !string.Equals(part.FirstName?.Text, changes.FirstName, StringComparison.Ordinal))
                {
                    part.FirstName = new TextField { Text = changes.FirstName };
                    changed = true;
                }

                if (changes.LastName is not null && !string.Equals(part.LastName?.Text, changes.LastName, StringComparison.Ordinal))
                {
                    part.LastName = new TextField { Text = changes.LastName };
                    changed = true;
                }
            });
        }

        if (changes.TimeZoneId is not null)
        {
            contentItem.Alter<OmnichannelContactPart>(part =>
            {
                if (!string.Equals(part.TimeZoneId, changes.TimeZoneId, StringComparison.Ordinal))
                {
                    part.TimeZoneId = changes.TimeZoneId;
                    changed = true;
                }
            });
        }

        return changed;
    }

    /// <summary>
    /// Applies the contact preferences.
    /// </summary>
    /// <param name="contentItem">The contact content item.</param>
    /// <param name="changes">The changes.</param>
    /// <returns><see langword="true"/> when something changed.</returns>
    private bool ApplyPreferences(ContentItem contentItem, OmnichannelContactChanges changes)
    {
        if (changes.DoNotCall is null && changes.DoNotSms is null && changes.DoNotEmail is null)
        {
            return false;
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var changed = false;

        contentItem.Alter<OmnichannelContactPart>(part =>
        {
            if (changes.DoNotCall is bool doNotCall && part.DoNotCall != doNotCall)
            {
                part.SetDoNotCall(doNotCall, utcNow);
                changed = true;
            }

            if (changes.DoNotSms is bool doNotSms && part.DoNotSms != doNotSms)
            {
                part.SetDoNotSms(doNotSms, utcNow);
                changed = true;
            }

            if (changes.DoNotEmail is bool doNotEmail && part.DoNotEmail != doNotEmail)
            {
                part.SetDoNotEmail(doNotEmail, utcNow);
                changed = true;
            }
        });

        return changed;
    }

    /// <summary>
    /// Records an email address on the contact, replacing the one on file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The item is created through the content manager rather than constructed directly, so it is built
    /// the way every other content item is: its type's parts and defaults are applied and it is given an
    /// identifier. An item with no identifier is not merely incomplete - a bag's items are keyed by
    /// content item id when an edit is applied, so one such item stops the entire bag from saving, and
    /// the failure is silent. A contact's phone numbers simply reverted on publish.
    /// </para>
    /// <para>
    /// It replaces rather than appends, because appending added a duplicate address every time.
    /// </para>
    /// </remarks>
    /// <param name="contentItem">The contact content item.</param>
    /// <param name="email">The address, or <see langword="null"/> to do nothing.</param>
    /// <returns><see langword="true"/> when the contact changed.</returns>
    private async Task<bool> UpsertEmailAsync(ContentItem contentItem, string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        email = email.Trim();

        // A conservative sanity check, so a mis-heard or mis-parsed phrase is never written as
        // somebody's email address.
        if (email.Length < 5 || !email.Contains('@', StringComparison.Ordinal) || email.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        var current = ContentItemOmnichannelContactProjection.Project(contentItem)?.GetPrimaryEmail();

        if (string.Equals(current, email, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var bag = contentItem.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods);
        bag.ContentItems ??= [];
        bag.ContentItems.RemoveAll(method =>
            string.Equals(method.ContentType, OmnichannelConstants.ContentTypes.EmailAddress, StringComparison.Ordinal));

        var emailItem = await _contentManager.NewAsync(OmnichannelConstants.ContentTypes.EmailAddress);
        emailItem.DisplayText = email;
        emailItem.Alter<EmailInfoPart>(part => part.Email = new TextField { Text = email });

        bag.ContentItems.Add(emailItem);
        contentItem.Apply(OmnichannelConstants.NamedParts.ContactMethods, bag);

        return true;
    }

    /// <summary>
    /// Builds the field a phone number is stored in.
    /// </summary>
    /// <remarks>
    /// The region is filled in alongside the E.164 value because the contact index needs it to work
    /// out the national digits it searches on. A field carrying only the E.164 value would index as
    /// unreachable, and an inbound call from that number would not find its contact.
    /// </remarks>
    /// <param name="number">The number, in E.164 form.</param>
    /// <returns>The field.</returns>
    private PhoneField BuildPhoneField(string number)
    {
        var field = new PhoneField { PhoneNumber = number };

        if (_phoneNumberService.TryParse(number, out var parsed))
        {
            field.PhoneNumber = parsed.Value;
            field.CountryCode = _phoneNumberService.GetRegionCode(parsed);
        }

        return field;
    }

    /// <summary>
    /// Records a phone number on the contact, replacing the one of the same kind.
    /// </summary>
    /// <param name="contentItem">The contact content item.</param>
    /// <param name="phoneNumber">The number, or <see langword="null"/> to do nothing.</param>
    /// <returns><see langword="true"/> when the contact changed.</returns>
    private async Task<bool> UpsertPhoneNumberAsync(ContentItem contentItem, ContactPhoneNumber phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber?.Number))
        {
            return false;
        }

        var number = phoneNumber.Number.Trim();
        var type = phoneNumber.Type;

        var bag = contentItem.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods);
        bag.ContentItems ??= [];

        var existing = bag.ContentItems.FirstOrDefault(method =>
            string.Equals(method.ContentType, OmnichannelConstants.ContentTypes.PhoneNumber, StringComparison.Ordinal) &&
            method.TryGet<PhoneNumberInfoPart>(out var part) &&
            string.Equals(part.Type?.Text, type, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            if (existing.TryGet<PhoneNumberInfoPart>(out var existingPart) &&
                string.Equals(existingPart.Number?.PhoneNumber, number, StringComparison.Ordinal))
            {
                return false;
            }

            bag.ContentItems.Remove(existing);
        }

        var phoneItem = await _contentManager.NewAsync(OmnichannelConstants.ContentTypes.PhoneNumber);
        phoneItem.DisplayText = number;
        phoneItem.Alter<PhoneNumberInfoPart>(part =>
        {
            part.Number = BuildPhoneField(number);
            part.Extension = new TextField { Text = phoneNumber.Extension };
            part.Type = new TextField { Text = type };
        });

        bag.ContentItems.Add(phoneItem);
        contentItem.Apply(OmnichannelConstants.NamedParts.ContactMethods, bag);

        return true;
    }
}
