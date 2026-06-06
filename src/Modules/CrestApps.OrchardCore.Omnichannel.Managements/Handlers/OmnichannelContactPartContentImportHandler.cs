using System.Data;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Handlers;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Flows.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Handles import and export of omnichannel contact communication methods
/// (email, cell phone, home phone) for the <see cref="OmnichannelContactPart"/>.
/// Maps these columns to and from the BagPart named "ContactMethods".
/// </summary>
public sealed class OmnichannelContactPartContentImportHandler : ContentImportHandlerBase, IContentPartImportHandler
{
    internal readonly IStringLocalizer S;
    private readonly IClock _clock;
    private readonly IPhoneNumberService _phoneNumberService;

    private ImportColumn _emailColumn;
    private ImportColumn _cellPhoneColumn;
    private ImportColumn _homePhoneColumn;
    private ImportColumn _doNotCallColumn;
    private ImportColumn _doNotCallUtcColumn;
    private ImportColumn _doNotSmsColumn;
    private ImportColumn _doNotSmsUtcColumn;
    private ImportColumn _doNotEmailColumn;
    private ImportColumn _doNotEmailUtcColumn;
    private ImportColumn _doNotChatColumn;
    private ImportColumn _doNotChatUtcColumn;
    private ImportColumn _timeZoneIdColumn;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactPartContentImportHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="phoneNumberService">The phone number service for E.164 formatting.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelContactPartContentImportHandler(
        IClock clock,
        IPhoneNumberService phoneNumberService,
        IStringLocalizer<OmnichannelContactPartContentImportHandler> stringLocalizer)
    {
        _clock = clock;
        _phoneNumberService = phoneNumberService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<ImportColumn> GetColumns(ImportContentPartContext context)
    {
        _emailColumn ??= new ImportColumn()
        {
            Name = $"{OmnichannelConstants.NamedParts.ContactMethods}_Email",
            Description = S["The primary email address for the contact."],
            AdditionalNames = ["Email", "EmailAddress", "Email Address"],
        };

        _cellPhoneColumn ??= new ImportColumn()
        {
            Name = $"{OmnichannelConstants.NamedParts.ContactMethods}_CellPhone",
            Description = S["The primary cell phone number for the contact."],
            AdditionalNames = ["CellPhone", "Cell Phone", "Cell", "Mobile", "MobilePhone", "Mobile Phone"],
        };

        _homePhoneColumn ??= new ImportColumn()
        {
            Name = $"{OmnichannelConstants.NamedParts.ContactMethods}_HomePhone",
            Description = S["The primary home/landline phone number for the contact."],
            AdditionalNames = ["HomePhone", "Home Phone", "Phone", "PhoneNumber", "Phone Number", "Landline"],
        };

        _doNotCallColumn ??= CreateBooleanPreferenceColumn(nameof(OmnichannelContactPart.DoNotCall), S["Whether phone calls are blocked for the contact. Use true or false."]);
        _doNotCallUtcColumn ??= CreatePreferenceColumn(nameof(OmnichannelContactPart.DoNotCallUtc), S["When the phone-call block was recorded in UTC."]);
        _doNotSmsColumn ??= CreateBooleanPreferenceColumn(nameof(OmnichannelContactPart.DoNotSms), S["Whether SMS is blocked for the contact. Use true or false."]);
        _doNotSmsUtcColumn ??= CreatePreferenceColumn(nameof(OmnichannelContactPart.DoNotSmsUtc), S["When the SMS block was recorded in UTC."]);
        _doNotEmailColumn ??= CreateBooleanPreferenceColumn(nameof(OmnichannelContactPart.DoNotEmail), S["Whether email is blocked for the contact. Use true or false."]);
        _doNotEmailUtcColumn ??= CreatePreferenceColumn(nameof(OmnichannelContactPart.DoNotEmailUtc), S["When the email block was recorded in UTC."]);
        _doNotChatColumn ??= CreateBooleanPreferenceColumn(nameof(OmnichannelContactPart.DoNotChat), S["Whether chat is blocked for the contact. Use true or false."]);
        _doNotChatUtcColumn ??= CreatePreferenceColumn(nameof(OmnichannelContactPart.DoNotChatUtc), S["When the chat block was recorded in UTC."]);
        _timeZoneIdColumn ??= new ImportColumn()
        {
            Name = nameof(OmnichannelContactPart.TimeZoneId),
            Description = S["The contact's IANA time zone identifier."],
            AdditionalNames = ["TimeZone", "Time Zone", "Timezone"],
        };

        return
        [
            _emailColumn,
            _cellPhoneColumn,
            _homePhoneColumn,
            _timeZoneIdColumn,
            _doNotCallColumn,
            _doNotCallUtcColumn,
            _doNotSmsColumn,
            _doNotSmsUtcColumn,
            _doNotEmailColumn,
            _doNotEmailUtcColumn,
            _doNotChatColumn,
            _doNotChatUtcColumn,
        ];
    }

    /// <inheritdoc/>
    public Task ImportAsync(ContentPartImportMapContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.ContentItem, nameof(context.ContentItem));
        ArgumentNullException.ThrowIfNull(context.Columns, nameof(context.Columns));
        ArgumentNullException.ThrowIfNull(context.Row, nameof(context.Row));

        _ = GetColumns(context);
        var countryCode = NormalizeCountryCode(context.Entry?.GetOrCreate<OmnichannelContactImportOptionsPart>()?.SelectedCountryCode);
        string email = null;
        string cellPhone = null;
        string homePhone = null;
        string rawTimeZoneId = null;
        string normalizedCellPhone = null;
        string normalizedHomePhone = null;
        var doNotCall = false;
        DateTime? doNotCallUtc = null;
        var hasDoNotCall = false;
        var doNotSms = false;
        DateTime? doNotSmsUtc = null;
        var hasDoNotSms = false;
        var doNotEmail = false;
        DateTime? doNotEmailUtc = null;
        var hasDoNotEmail = false;
        var doNotChat = false;
        DateTime? doNotChatUtc = null;
        var hasDoNotChat = false;
        var hasTimeZoneId = false;

        foreach (DataColumn column in context.Columns)
        {
            if (Is(column.ColumnName, _emailColumn))
            {
                email = context.Row[column]?.ToString()?.Trim();
            }
            else if (Is(column.ColumnName, _cellPhoneColumn))
            {
                cellPhone = context.Row[column]?.ToString()?.Trim();
            }
            else if (Is(column.ColumnName, _homePhoneColumn))
            {
                homePhone = context.Row[column]?.ToString()?.Trim();
            }
            else if (Is(column.ColumnName, _timeZoneIdColumn))
            {
                hasTimeZoneId = true;
                rawTimeZoneId = context.Row[column]?.ToString()?.Trim();
            }
            else if (Is(column.ColumnName, _doNotCallColumn))
            {
                hasDoNotCall = TryParseBoolean(context.Row[column], out doNotCall);
            }
            else if (Is(column.ColumnName, _doNotCallUtcColumn))
            {
                doNotCallUtc = TryParseDateTime(context.Row[column]);
            }
            else if (Is(column.ColumnName, _doNotSmsColumn))
            {
                hasDoNotSms = TryParseBoolean(context.Row[column], out doNotSms);
            }
            else if (Is(column.ColumnName, _doNotSmsUtcColumn))
            {
                doNotSmsUtc = TryParseDateTime(context.Row[column]);
            }
            else if (Is(column.ColumnName, _doNotEmailColumn))
            {
                hasDoNotEmail = TryParseBoolean(context.Row[column], out doNotEmail);
            }
            else if (Is(column.ColumnName, _doNotEmailUtcColumn))
            {
                doNotEmailUtc = TryParseDateTime(context.Row[column]);
            }
            else if (Is(column.ColumnName, _doNotChatColumn))
            {
                hasDoNotChat = TryParseBoolean(context.Row[column], out doNotChat);
            }
            else if (Is(column.ColumnName, _doNotChatUtcColumn))
            {
                doNotChatUtc = TryParseDateTime(context.Row[column]);
            }
        }

        if (string.IsNullOrEmpty(email) &&
            string.IsNullOrEmpty(cellPhone) &&
            string.IsNullOrEmpty(homePhone) &&
            !hasTimeZoneId &&
            !hasDoNotCall &&
            !hasDoNotSms &&
            !hasDoNotEmail &&
            !hasDoNotChat &&
            !doNotCallUtc.HasValue &&
            !doNotSmsUtc.HasValue &&
            !doNotEmailUtc.HasValue &&
            !doNotChatUtc.HasValue)
        {
            return Task.CompletedTask;
        }

        if (!string.IsNullOrEmpty(email) || !string.IsNullOrEmpty(cellPhone) || !string.IsNullOrEmpty(homePhone))
        {
            var bagPart = context.ContentItem.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods);
            bagPart.ContentItems ??= [];

            if (!string.IsNullOrEmpty(email))
            {
                RemoveEmailAddressContentItems(bagPart);
                var emailItem = CreateEmailAddressContentItem(email);
                bagPart.ContentItems.Add(emailItem);
            }

            if (!string.IsNullOrEmpty(cellPhone))
            {
                RemovePhoneNumberContentItems(bagPart, "Cell");
                normalizedCellPhone = NormalizePhoneNumber(cellPhone, countryCode);
                var cellPhoneItem = CreatePhoneNumberContentItem(normalizedCellPhone, "Cell");
                bagPart.ContentItems.Add(cellPhoneItem);
            }

            if (!string.IsNullOrEmpty(homePhone))
            {
                RemovePhoneNumberContentItems(bagPart, "Home");
                normalizedHomePhone = NormalizePhoneNumber(homePhone, countryCode);
                var homePhoneItem = CreatePhoneNumberContentItem(normalizedHomePhone, "Home");
                bagPart.ContentItems.Add(homePhoneItem);
            }

            context.ContentItem.Apply(OmnichannelConstants.NamedParts.ContactMethods, bagPart);
        }

        var contactPart = context.ContentItem.GetOrCreate<OmnichannelContactPart>();
        var utcNow = _clock.UtcNow;

        if (hasDoNotCall)
        {
            contactPart.SetDoNotCall(doNotCall, utcNow);
            contactPart.DoNotCallUtc = doNotCall ? doNotCallUtc ?? contactPart.DoNotCallUtc : null;
        }

        if (hasDoNotSms)
        {
            contactPart.SetDoNotSms(doNotSms, utcNow);
            contactPart.DoNotSmsUtc = doNotSms ? doNotSmsUtc ?? contactPart.DoNotSmsUtc : null;
        }

        if (hasDoNotEmail)
        {
            contactPart.SetDoNotEmail(doNotEmail, utcNow);
            contactPart.DoNotEmailUtc = doNotEmail ? doNotEmailUtc ?? contactPart.DoNotEmailUtc : null;
        }

        if (hasDoNotChat)
        {
            contactPart.SetDoNotChat(doNotChat, utcNow);
            contactPart.DoNotChatUtc = doNotChat ? doNotChatUtc ?? contactPart.DoNotChatUtc : null;
        }

        if (hasTimeZoneId)
        {
            if (string.IsNullOrEmpty(rawTimeZoneId))
            {
                contactPart.TimeZoneId = null;
            }
            else
            {
                var normalizedTimeZoneId = OmnichannelTimeZoneHelper.NormalizeTimeZoneId(_clock, rawTimeZoneId);

                if (!string.IsNullOrEmpty(normalizedTimeZoneId))
                {
                    contactPart.TimeZoneId = normalizedTimeZoneId;
                }
            }
        }
        else if (string.IsNullOrEmpty(contactPart.TimeZoneId))
        {
            contactPart.TimeZoneId = InferTimeZoneId(normalizedHomePhone, normalizedCellPhone);
        }

        context.ContentItem.Apply(contactPart);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ExportAsync(ContentPartExportMapContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.ContentItem, nameof(context.ContentItem));
        ArgumentNullException.ThrowIfNull(context.Row, nameof(context.Row));

        _ = GetColumns(null);

        var hasContactMethods = context.ContentItem.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bagPart) &&
            bagPart.ContentItems is not null &&
            bagPart.ContentItems.Count > 0;
        var hasContactPart = context.ContentItem.TryGet<OmnichannelContactPart>(out var contactPart);

        if (!hasContactMethods && !hasContactPart)
        {
            return Task.CompletedTask;
        }

        string email = null;
        string cellPhone = null;
        string homePhone = null;

        if (hasContactMethods)
        {
            foreach (var contentMethod in bagPart.ContentItems)
            {
                if (email == null &&
                    contentMethod.ContentType == OmnichannelConstants.ContentTypes.EmailAddress &&
                    contentMethod.TryGet<EmailInfoPart>(out var emailPart) &&
                    !string.IsNullOrEmpty(emailPart.Email?.Text))
                {
                    email = emailPart.Email.Text;
                }

                if (cellPhone == null &&
                    contentMethod.ContentType == OmnichannelConstants.ContentTypes.PhoneNumber &&
                    contentMethod.TryGet<PhoneNumberInfoPart>(out var cellPart) &&
                    cellPart.Type?.Text == "Cell" &&
                    !string.IsNullOrEmpty(cellPart.Number?.Text))
                {
                    cellPhone = cellPart.Number.Text;
                }

                if (homePhone == null &&
                    contentMethod.ContentType == OmnichannelConstants.ContentTypes.PhoneNumber &&
                    contentMethod.TryGet<PhoneNumberInfoPart>(out var homePart) &&
                    homePart.Type?.Text == "Home" &&
                    !string.IsNullOrEmpty(homePart.Number?.Text))
                {
                    homePhone = homePart.Number.Text;
                }

                if (email != null && cellPhone != null && homePhone != null)
                {
                    break;
                }
            }
        }

        if (_emailColumn?.Name != null && email != null)
        {
            context.Row[_emailColumn.Name] = email;
        }

        if (_cellPhoneColumn?.Name != null && cellPhone != null)
        {
            context.Row[_cellPhoneColumn.Name] = cellPhone;
        }

        if (_homePhoneColumn?.Name != null && homePhone != null)
        {
            context.Row[_homePhoneColumn.Name] = homePhone;
        }

        if (hasContactPart)
        {
            if (!string.IsNullOrEmpty(contactPart.TimeZoneId))
            {
                context.Row[_timeZoneIdColumn.Name] = contactPart.TimeZoneId;
            }

            context.Row[_doNotCallColumn.Name] = contactPart.DoNotCall;
            context.Row[_doNotSmsColumn.Name] = contactPart.DoNotSms;
            context.Row[_doNotEmailColumn.Name] = contactPart.DoNotEmail;
            context.Row[_doNotChatColumn.Name] = contactPart.DoNotChat;

            if (contactPart.DoNotCallUtc.HasValue)
            {
                context.Row[_doNotCallUtcColumn.Name] = contactPart.DoNotCallUtc.Value;
            }

            if (contactPart.DoNotSmsUtc.HasValue)
            {
                context.Row[_doNotSmsUtcColumn.Name] = contactPart.DoNotSmsUtc.Value;
            }

            if (contactPart.DoNotEmailUtc.HasValue)
            {
                context.Row[_doNotEmailUtcColumn.Name] = contactPart.DoNotEmailUtc.Value;
            }

            if (contactPart.DoNotChatUtc.HasValue)
            {
                context.Row[_doNotChatUtcColumn.Name] = contactPart.DoNotChatUtc.Value;
            }
        }

        return Task.CompletedTask;
    }

    private static ImportColumn CreatePreferenceColumn(string name, LocalizedString description)
    {
        return new ImportColumn()
        {
            Name = name,
            Description = description,
        };
    }

    private static ImportColumn CreateBooleanPreferenceColumn(string name, LocalizedString description)
    {
        return new ImportColumn()
        {
            Name = name,
            Description = description,
            ValidValues = ["true", "false"],
        };
    }

    private static bool TryParseBoolean(object value, out bool result)
    {
        result = false;

        if (value is null || value == DBNull.Value)
        {
            return false;
        }

        if (value is bool boolValue)
        {
            result = boolValue;
            return true;
        }

        var text = value.ToString()?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (bool.TryParse(text, out result))
        {
            return true;
        }

        if (string.Equals(text, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(text, "y", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }

        if (string.Equals(text, "0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(text, "no", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(text, "n", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }

        return false;
    }

    private static DateTime? TryParseDateTime(object value)
    {
        if (value is null || value == DBNull.Value)
        {
            return null;
        }

        if (value is DateTime dateTime)
        {
            return dateTime;
        }

        var text = value.ToString()?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return DateTime.TryParse(text, out var parsedDateTime) ? parsedDateTime : null;
    }

    private string NormalizePhoneNumber(string phoneNumber, string countryCode)
        => _phoneNumberService.TryFormatToE164(phoneNumber, GetFormattingRegionCode(phoneNumber, countryCode), out var e164Number)
            ? e164Number
            : phoneNumber;

    private string InferTimeZoneId(string primaryPhoneNumber, string fallbackPhoneNumber)
        => GetFirstKnownTimeZoneId(primaryPhoneNumber) ?? GetFirstKnownTimeZoneId(fallbackPhoneNumber);

    private string GetFirstKnownTimeZoneId(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return null;
        }

        foreach (var timeZoneId in _phoneNumberService.GetTimeZones(phoneNumber))
        {
            var normalizedTimeZoneId = OmnichannelTimeZoneHelper.NormalizeTimeZoneId(_clock, timeZoneId);

            if (!string.IsNullOrEmpty(normalizedTimeZoneId))
            {
                return normalizedTimeZoneId;
            }
        }

        return null;
    }

    private static string NormalizeCountryCode(string countryCode)
        => string.IsNullOrWhiteSpace(countryCode)
            ? null
            : countryCode.Trim().ToUpperInvariant();

    private static string GetFormattingRegionCode(string phoneNumber, string countryCode)
        => !string.IsNullOrWhiteSpace(phoneNumber) && phoneNumber.TrimStart().StartsWith('+')
            ? null
            : countryCode;

    private static void RemoveEmailAddressContentItems(BagPart bagPart)
        => bagPart.ContentItems.RemoveAll(contentMethod =>
            contentMethod.ContentType == OmnichannelConstants.ContentTypes.EmailAddress);

    private static void RemovePhoneNumberContentItems(BagPart bagPart, string type)
        => bagPart.ContentItems.RemoveAll(contentMethod =>
            contentMethod.ContentType == OmnichannelConstants.ContentTypes.PhoneNumber &&
            contentMethod.TryGet<PhoneNumberInfoPart>(out var phonePart) &&
            string.Equals(phonePart.Type?.Text, type, StringComparison.Ordinal));

    private static ContentItem CreateEmailAddressContentItem(string email)
    {
        var contentItem = new ContentItem();
        contentItem.ContentType = OmnichannelConstants.ContentTypes.EmailAddress;
        contentItem.DisplayText = email;

        contentItem.Alter<EmailInfoPart>(part =>
        {
            part.Email = new TextField { Text = email };
        });

        return contentItem;
    }

    private static ContentItem CreatePhoneNumberContentItem(string number, string type)
    {
        var contentItem = new ContentItem();
        contentItem.ContentType = OmnichannelConstants.ContentTypes.PhoneNumber;
        contentItem.DisplayText = $"{type}: {number}";

        contentItem.Alter<PhoneNumberInfoPart>(part =>
        {
            part.Number = new TextField { Text = number };
            part.Type = new TextField { Text = type };
        });

        return contentItem;
    }
}
