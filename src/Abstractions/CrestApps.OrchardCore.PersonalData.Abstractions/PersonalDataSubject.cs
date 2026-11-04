using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.PersonalData;

/// <summary>
/// The person a privacy request is about, known by user IDs, emails and/or phone numbers. Contributors add what their
/// records know about the person while it is identified, so every module finds every record.
/// </summary>
public sealed class PersonalDataSubject
{
    private const int MinimumNameLength = 2;
    private const int MinimumPhoneDigits = 7;
    private const string PhoneSeparators = "+-(). ";

    private readonly List<string> _userIds = [];
    private readonly List<string> _emails = [];
    private readonly List<string> _phoneNumbers = [];
    private readonly List<string> _names = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalDataSubject"/> class.
    /// </summary>
    /// <param name="userId">The person's user ID, when they have an account.</param>
    /// <param name="email">The person's email.</param>
    /// <param name="phoneNumber">The person's phone number, preferably in E.164 form.</param>
    public PersonalDataSubject(string userId = null, string email = null, string phoneNumber = null)
    {
        if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new ArgumentException("A user ID, an email or a phone number is required to find a person's data.", nameof(userId));
        }

        AddUserId(userId);
        AddEmail(email);
        AddPhoneNumber(phoneNumber);

        // The data the request starts from is not a change found while identifying the person.
        Version = 0;
    }

    /// <summary>
    /// Gets the user ID the request started from, or the first one found.
    /// </summary>
    public string UserId
        => _userIds.FirstOrDefault();

    /// <summary>
    /// Gets the email the request started from, or the first one found.
    /// </summary>
    public string Email
        => _emails.FirstOrDefault();

    /// <summary>
    /// Gets the phone number the request started from, or the first one found.
    /// </summary>
    public string PhoneNumber
        => _phoneNumbers.FirstOrDefault();

    /// <summary>
    /// Gets every user ID the person is known by.
    /// </summary>
    public IReadOnlyList<string> UserIds
        => _userIds;

    /// <summary>
    /// Gets every email the person is known by.
    /// </summary>
    public IReadOnlyList<string> Emails
        => _emails;

    /// <summary>
    /// Gets every phone number the person is known by.
    /// </summary>
    public IReadOnlyList<string> PhoneNumbers
        => _phoneNumbers;

    /// <summary>
    /// Gets the names the person is known by, which are scrubbed from free text.
    /// </summary>
    public IReadOnlyList<string> Names
        => _names;

    /// <summary>
    /// Gets a number that grows each time something new is learned about the person.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>
    /// Adds a user ID the person is known by.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns><see langword="true"/> when the user ID was new.</returns>
    public bool AddUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || IsUser(userId))
        {
            return false;
        }

        _userIds.Add(userId.Trim());

        return Changed();
    }

    /// <summary>
    /// Adds an email the person is known by.
    /// </summary>
    /// <param name="email">The email.</param>
    /// <returns><see langword="true"/> when the email was new.</returns>
    public bool AddEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || IsEmail(email))
        {
            return false;
        }

        _emails.Add(email.Trim());

        return Changed();
    }

    /// <summary>
    /// Adds a phone number the person is known by.
    /// </summary>
    /// <param name="phoneNumber">The phone number.</param>
    /// <returns><see langword="true"/> when the phone number was new.</returns>
    public bool AddPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber) || DigitsOf(phoneNumber).Length == 0 || IsPhoneNumber(phoneNumber))
        {
            return false;
        }

        _phoneNumbers.Add(phoneNumber.Trim());

        return Changed();
    }

    /// <summary>
    /// Adds a name the person is known by. Names shorter than two characters are ignored, because scrubbing them would
    /// damage unrelated text.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when the name was new.</returns>
    public bool AddName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var trimmed = name.Trim();

        if (trimmed.Length < MinimumNameLength || _names.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        _names.Add(trimmed);

        return Changed();
    }

    /// <summary>
    /// Returns whether a user ID is the person's.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns><see langword="true"/> when it is one of the person's user IDs.</returns>
    public bool IsUser(string userId)
        => !string.IsNullOrWhiteSpace(userId) && _userIds.Contains(userId.Trim(), StringComparer.Ordinal);

    /// <summary>
    /// Returns whether an email is the person's, ignoring case.
    /// </summary>
    /// <param name="email">The email.</param>
    /// <returns><see langword="true"/> when it is one of the person's emails.</returns>
    public bool IsEmail(string email)
        => !string.IsNullOrWhiteSpace(email) && _emails.Contains(email.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns whether a phone number is the person's, comparing digits only, so <c>+1 (702) 555-0100</c> matches
    /// <c>+17025550100</c>.
    /// </summary>
    /// <param name="phoneNumber">The phone number.</param>
    /// <returns><see langword="true"/> when it is one of the person's phone numbers.</returns>
    public bool IsPhoneNumber(string phoneNumber)
    {
        var digits = DigitsOf(phoneNumber);

        return digits.Length > 0 && _phoneNumbers.Any(known => string.Equals(DigitsOf(known), digits, StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns whether free text mentions one of the person's emails, phone numbers or names.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when the text mentions the person.</returns>
    public bool IsMentionedIn(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return Terms().Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Replaces the person's emails, phone numbers and names in free text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="replacement">What they are replaced with, or <see langword="null"/> to remove them.</param>
    /// <returns>The text without the person's details.</returns>
    public string Scrub(string text, string replacement)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // Longer terms go first, so a full name is replaced before a shorter name it contains.
        foreach (var term in Terms().OrderByDescending(term => term.Length))
        {
            text = text.Replace(term, replacement ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return text;
    }

    /// <summary>
    /// Returns whether a value is one of the person's phone numbers written another way, such as only the national
    /// number <c>7025550100</c> of <c>+17025550100</c>. Values with fewer than seven digits, or with letters, never match.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the value is one of the person's phone numbers.</returns>
    public bool MatchesPhoneNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(character => !char.IsAsciiDigit(character) && !PhoneSeparators.Contains(character)))
        {
            return false;
        }

        var digits = DigitsOf(value);

        return digits.Length >= MinimumPhoneDigits &&
            _phoneNumbers.Any(known => DigitsOf(known).EndsWith(digits, StringComparison.Ordinal));
    }

    /// <summary>
    /// Replaces the person's details in every string of a JSON document, such as the content of a content item: text
    /// that mentions an email, a phone number or a name is scrubbed, and a value that is one of the person's phone
    /// numbers is cleared.
    /// </summary>
    /// <param name="node">The JSON document.</param>
    /// <param name="replacement">What the details are replaced with, or <see langword="null"/> to remove them.</param>
    /// <returns><see langword="true"/> when anything changed.</returns>
    public bool ScrubJson(JsonNode node, string replacement)
    {
        var changed = false;

        if (node is JsonObject jsonObject)
        {
            // The keys are copied first, because replacing a value while enumerating the object is not allowed.
            foreach (var key in jsonObject.Select(property => property.Key).ToArray())
            {
                changed |= ScrubChild(jsonObject[key], replacement, value => jsonObject[key] = value);
            }
        }
        else if (node is JsonArray jsonArray)
        {
            for (var i = 0; i < jsonArray.Count; i++)
            {
                var index = i;

                changed |= ScrubChild(jsonArray[index], replacement, value => jsonArray[index] = value);
            }
        }

        return changed;
    }

    private bool ScrubChild(JsonNode child, string replacement, Action<string> replace)
    {
        if (child is JsonValue value && value.TryGetValue<string>(out var text))
        {
            if (MatchesPhoneNumber(text))
            {
                replace(null);

                return true;
            }

            if (!IsMentionedIn(text))
            {
                return false;
            }

            replace(Scrub(text, replacement));

            return true;
        }

        return child is not null && ScrubJson(child, replacement);
    }

    private IEnumerable<string> Terms()
        => _emails.Concat(_phoneNumbers).Concat(_names);

    private bool Changed()
    {
        Version++;

        return true;
    }

    private static string DigitsOf(string phoneNumber)
        => string.IsNullOrEmpty(phoneNumber)
            ? string.Empty
            : new string([.. phoneNumber.Where(char.IsAsciiDigit)]);
}
