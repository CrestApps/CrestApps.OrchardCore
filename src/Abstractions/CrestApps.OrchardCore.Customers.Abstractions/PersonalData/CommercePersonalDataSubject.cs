using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Customers.Models;

namespace CrestApps.OrchardCore.Customers.PersonalData;

/// <summary>
/// The person an export or anonymization is about: the owner and email the request named, and everything the
/// contributors found that also identifies them, such as the guest owners of their checkouts and the names they gave.
/// </summary>
/// <remarks>
/// A guest buyer is owned by an id derived from their checkout, so one person who checked out three times as a guest
/// has three guest owners. Only the email ties them together, which is why contributors add what they find here
/// before any record is exported or anonymized.
/// </remarks>
public sealed class CommercePersonalDataSubject
{
    private const int MinimumNameLength = 2;

    private readonly HashSet<CustomerOwner> _owners = [];
    private readonly HashSet<string> _emails = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _checkoutSessionIds = new(StringComparer.Ordinal);
    private readonly HashSet<CommerceProviderCustomer> _providerCustomers = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="CommercePersonalDataSubject"/> class.
    /// </summary>
    /// <param name="owner">The person's account or guest owner, or <see langword="null"/>.</param>
    /// <param name="email">The person's email, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">Neither an owner nor an email is given.</exception>
    public CommercePersonalDataSubject(CustomerOwner owner, string email)
    {
        if (owner is null && string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("An owner or an email is required to find a person's commerce data.", nameof(email));
        }

        Owner = owner;
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();

        if (owner is not null)
        {
            _owners.Add(owner);
        }

        if (Email is not null)
        {
            _emails.Add(Email);
        }
    }

    /// <summary>
    /// Gets the owner the request named, or <see langword="null"/>.
    /// </summary>
    public CustomerOwner Owner { get; }

    /// <summary>
    /// Gets the email the request named, trimmed, or <see langword="null"/>.
    /// </summary>
    public string Email { get; }

    /// <summary>
    /// Gets every owner known to be the person: the requested one and the guest owners of their checkouts.
    /// </summary>
    public IReadOnlyCollection<CustomerOwner> Owners
        => _owners;

    /// <summary>
    /// Gets the user ids among <see cref="Owners"/>.
    /// </summary>
    public IEnumerable<string> UserIds
        => _owners.Where(owner => owner.Kind == CustomerOwnerKind.Authenticated).Select(owner => owner.Id);

    /// <summary>
    /// Gets the guest owner ids among <see cref="Owners"/>.
    /// </summary>
    public IEnumerable<string> GuestOwnerIds
        => _owners.Where(owner => owner.Kind == CustomerOwnerKind.Guest).Select(owner => owner.Id);

    /// <summary>
    /// Gets every email known to be the person's, compared without case sensitivity.
    /// </summary>
    public IReadOnlyCollection<string> Emails
        => _emails;

    /// <summary>
    /// Gets every name the person gave, compared without case sensitivity.
    /// </summary>
    public IReadOnlyCollection<string> Names
        => _names;

    /// <summary>
    /// Gets the identifiers of the person's checkout sessions.
    /// </summary>
    public IReadOnlyCollection<string> CheckoutSessionIds
        => _checkoutSessionIds;

    /// <summary>
    /// Gets the customers payment providers keep for the person, such as a Stripe customer.
    /// </summary>
    public IReadOnlyCollection<CommerceProviderCustomer> ProviderCustomers
        => _providerCustomers;

    /// <summary>
    /// Gets a number that grows whenever something new is added, so the caller knows whether a round of identification
    /// found anything.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>
    /// Adds an owner known to be the person.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <returns><see langword="true"/> when it was not known yet.</returns>
    public bool AddOwner(CustomerOwner owner)
        => owner is not null && Track(_owners.Add(owner));

    /// <summary>
    /// Adds an email known to be the person's.
    /// </summary>
    /// <param name="email">The email.</param>
    /// <returns><see langword="true"/> when it was not known yet.</returns>
    public bool AddEmail(string email)
        => !string.IsNullOrWhiteSpace(email) && Track(_emails.Add(email.Trim()));

    /// <summary>
    /// Adds a name the person gave, so it is removed wherever it appears in free text such as notes.
    /// </summary>
    /// <param name="name">The name. A blank or single-character name is ignored.</param>
    /// <returns><see langword="true"/> when it was not known yet.</returns>
    public bool AddName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var trimmed = name.Trim();

        return trimmed.Length >= MinimumNameLength && Track(_names.Add(trimmed));
    }

    /// <summary>
    /// Adds one of the person's checkout sessions.
    /// </summary>
    /// <param name="sessionId">The checkout session identifier.</param>
    /// <returns><see langword="true"/> when it was not known yet.</returns>
    public bool AddCheckoutSession(string sessionId)
        => !string.IsNullOrEmpty(sessionId) && Track(_checkoutSessionIds.Add(sessionId));

    /// <summary>
    /// Adds a customer a payment provider keeps for the person.
    /// </summary>
    /// <param name="providerKey">The key of the payment provider, for example <c>Stripe</c>.</param>
    /// <param name="customerReference">The provider's identifier of the customer.</param>
    /// <returns><see langword="true"/> when it was not known yet.</returns>
    public bool AddProviderCustomer(string providerKey, string customerReference)
    {
        if (string.IsNullOrEmpty(providerKey) || string.IsNullOrEmpty(customerReference))
        {
            return false;
        }

        return Track(_providerCustomers.Add(new CommerceProviderCustomer(providerKey, customerReference)));
    }

    /// <summary>
    /// Returns whether the owner is known to be the person.
    /// </summary>
    /// <param name="kind">The owner kind.</param>
    /// <param name="ownerId">The owner id.</param>
    public bool IsOwner(CustomerOwnerKind kind, string ownerId)
        => !string.IsNullOrEmpty(ownerId) && _owners.Any(owner => owner.Kind == kind && string.Equals(owner.Id, ownerId, StringComparison.Ordinal));

    /// <summary>
    /// Returns whether the email is known to be the person's, without case sensitivity.
    /// </summary>
    /// <param name="email">The email to check.</param>
    public bool IsEmail(string email)
        => !string.IsNullOrWhiteSpace(email) && _emails.Contains(email.Trim());

    /// <summary>
    /// Returns whether the text contains one of the person's emails or names.
    /// </summary>
    /// <param name="text">The text to check.</param>
    public bool IsMentionedIn(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return _emails.Any(email => text.Contains(email, StringComparison.OrdinalIgnoreCase)) ||
            _names.Any(name => text.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Replaces each of the person's emails and names in the text, without case sensitivity.
    /// </summary>
    /// <param name="text">The text to scrub.</param>
    /// <param name="replacement">The text each match is replaced with, or <see langword="null"/> to remove it.</param>
    /// <returns>The scrubbed text, or <see langword="null"/> when <paramref name="text"/> is <see langword="null"/>.</returns>
    public string Scrub(string text, string replacement)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // Longer terms go first, so a full name is replaced before a shorter name it contains.
        foreach (var term in _emails.Concat(_names).OrderByDescending(term => term.Length))
        {
            text = text.Replace(term, replacement ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return text;
    }

    /// <summary>
    /// Replaces each of the person's emails and names in every string value of a JSON tree, in place.
    /// </summary>
    /// <param name="node">The JSON to scrub.</param>
    /// <param name="replacement">The text each match is replaced with, or <see langword="null"/> to remove it.</param>
    /// <returns><see langword="true"/> when any value changed.</returns>
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
            if (!IsMentionedIn(text))
            {
                return false;
            }

            replace(Scrub(text, replacement));

            return true;
        }

        return child is not null && ScrubJson(child, replacement);
    }

    private bool Track(bool added)
    {
        if (added)
        {
            Version++;
        }

        return added;
    }
}
