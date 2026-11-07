using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Clears the known personal-data fields of a CRM contact or lead: its display text and title, the first, middle and
/// last name fields of its parts, the emails and phone numbers of its <c>ContactMethods</c> bag, and any other field
/// whose whole value is one of the contact's emails or phone numbers.
/// </summary>
/// <remarks>
/// Nothing is replaced inside longer text, and a value under a property whose name ends with <c>Id</c> or <c>Ids</c> is
/// never read, so the contact's references (its company, owner, status and the other content items and users it points
/// at) stay exactly as they were.
/// </remarks>
internal static class CrmContactPersonalDataFields
{
    private const int MinimumPhoneDigits = 7;
    private const string PhoneCharacters = "+-(). ";

    /// <summary>
    /// The names of the text fields that hold a person's name, on any part of the contact.
    /// </summary>
    public static readonly IReadOnlyList<string> NameFields =
    [
        "FirstName",
        "MiddleName",
        "LastName",
        "FullName",
        "PreferredName",
        "Nickname",
    ];

    /// <summary>
    /// Clears the contact's known personal-data fields.
    /// </summary>
    /// <param name="contact">The contact or lead.</param>
    /// <param name="methods">The contact's emails and phone numbers, read before anything is cleared.</param>
    /// <param name="displayText">What the display text and title become.</param>
    public static void Clear(ContentItem contact, ContactMethodSet methods, string displayText)
    {
        ArgumentNullException.ThrowIfNull(contact);

        contact.DisplayText = displayText;

        var content = (JsonObject)contact.Content;

        if (content["TitlePart"] is JsonObject titlePart && titlePart.ContainsKey("Title"))
        {
            titlePart["Title"] = displayText;
        }

        ClearContactMethods(contact);
        ClearNameFields((JsonObject)contact.Content);

        var emails = methods?.EmailAddresses.Select(email => email?.Email).Where(email => !string.IsNullOrWhiteSpace(email)).ToArray() ?? [];
        var phoneDigits = methods?.PhoneNumbers.Select(phone => DigitsOf(phone?.PhoneNumber)).Where(digits => digits.Length >= MinimumPhoneDigits).ToArray() ?? [];

        ClearValues((JsonObject)contact.Content, emails, phoneDigits);
    }

    private static void ClearContactMethods(ContentItem contact)
    {
        if (!contact.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bag) || bag.ContentItems is not { Count: > 0 })
        {
            return;
        }

        foreach (var method in bag.ContentItems)
        {
            if (method is null)
            {
                continue;
            }

            if (method.Has<PhoneNumberInfoPart>())
            {
                method.Alter<PhoneNumberInfoPart>(part =>
                {
                    part.Number = null;
                    part.Extension = null;
                });
            }

            if (method.Has<EmailInfoPart>())
            {
                method.Alter<EmailInfoPart>(part => part.Email = null);
            }
        }

        contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, bag);
    }

    private static void ClearNameFields(JsonObject content)
    {
        foreach (var part in content.Select(property => property.Value).OfType<JsonObject>())
        {
            foreach (var name in NameFields)
            {
                if (part[name] is JsonObject field && field.ContainsKey("Text"))
                {
                    field["Text"] = null;
                }
            }
        }
    }

    private static void ClearValues(JsonNode node, string[] emails, string[] phoneDigits)
    {
        if (node is JsonObject jsonObject)
        {
            // The keys are copied first, because replacing a value while enumerating the object is not allowed.
            foreach (var key in jsonObject.Select(property => property.Key).ToArray())
            {
                if (key.EndsWith("Id", StringComparison.Ordinal) || key.EndsWith("Ids", StringComparison.Ordinal))
                {
                    continue;
                }

                if (IsPersonsValue(jsonObject[key], emails, phoneDigits))
                {
                    jsonObject[key] = null;
                }
                else
                {
                    ClearValues(jsonObject[key], emails, phoneDigits);
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            for (var i = 0; i < jsonArray.Count; i++)
            {
                if (IsPersonsValue(jsonArray[i], emails, phoneDigits))
                {
                    jsonArray[i] = null;
                }
                else
                {
                    ClearValues(jsonArray[i], emails, phoneDigits);
                }
            }
        }
    }

    // The whole value is one of the contact's emails, or one of its phone numbers however it is written (the full
    // number or its national part, with or without separators).
    private static bool IsPersonsValue(JsonNode node, string[] emails, string[] phoneDigits)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();

        if (emails.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trimmed.Any(character => !char.IsAsciiDigit(character) && !PhoneCharacters.Contains(character)))
        {
            return false;
        }

        var digits = DigitsOf(trimmed);

        return digits.Length >= MinimumPhoneDigits && phoneDigits.Any(known => known.EndsWith(digits, StringComparison.Ordinal));
    }

    private static string DigitsOf(string text)
        => string.IsNullOrEmpty(text) ? string.Empty : new string([.. text.Where(char.IsAsciiDigit)]);
}
