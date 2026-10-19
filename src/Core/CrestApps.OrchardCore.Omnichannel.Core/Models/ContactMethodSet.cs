namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// A contact's phone numbers and email addresses, in the order the contact keeps them.
/// </summary>
/// <remarks>
/// The order matters: the first number of a type is the contact's number of that type, and the first email address
/// is the contact's email address. Read it through <c>IContactMethodsAccessor</c>, which gives every reader the
/// same set whatever the contact's type keeps its contact methods in.
/// </remarks>
public sealed class ContactMethodSet
{
    /// <summary>
    /// A set with no phone numbers and no email addresses.
    /// </summary>
    public static ContactMethodSet Empty { get; } = new([], []);

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactMethodSet"/> class.
    /// </summary>
    /// <param name="phoneNumbers">The contact's phone numbers, in order.</param>
    /// <param name="emailAddresses">The contact's email addresses, in order.</param>
    public ContactMethodSet(IReadOnlyList<ContactPhoneNumber> phoneNumbers, IReadOnlyList<ContactEmailAddress> emailAddresses)
    {
        PhoneNumbers = phoneNumbers ?? [];
        EmailAddresses = emailAddresses ?? [];
    }

    /// <summary>
    /// Gets the contact's phone numbers, in order.
    /// </summary>
    public IReadOnlyList<ContactPhoneNumber> PhoneNumbers { get; }

    /// <summary>
    /// Gets the contact's email addresses, in order.
    /// </summary>
    public IReadOnlyList<ContactEmailAddress> EmailAddresses { get; }

    /// <summary>
    /// The contact's email address: the first one it keeps, or <see langword="null"/> when it has none.
    /// </summary>
    public string GetPrimaryEmailAddress()
    {
        foreach (var emailAddress in EmailAddresses)
        {
            if (!string.IsNullOrEmpty(emailAddress?.Email))
            {
                return emailAddress.Email;
            }
        }

        return null;
    }

    /// <summary>
    /// The first of the contact's numbers of a type, passing over the numbers that cannot be used.
    /// </summary>
    /// <param name="type">The phone type, matched as <see cref="ContactPhoneTypes.Normalize"/> spells it.</param>
    /// <param name="isExcluded">Whether a number must be passed over, or <see langword="null"/> to use every number.</param>
    public ContactPhoneNumber GetFirstPhoneNumber(string type, Func<string, bool> isExcluded = null)
    {
        var normalizedType = ContactPhoneTypes.Normalize(type);

        foreach (var phoneNumber in PhoneNumbers)
        {
            if (phoneNumber is null ||
                string.IsNullOrEmpty(phoneNumber.PhoneNumber) ||
                !string.Equals(ContactPhoneTypes.Normalize(phoneNumber.Type), normalizedType, StringComparison.Ordinal) ||
                isExcluded?.Invoke(phoneNumber.PhoneNumber) == true)
            {
                continue;
            }

            return phoneNumber;
        }

        return null;
    }

    /// <summary>
    /// The number the contact is called on: the first number of the type called first, in the order of
    /// <see cref="ContactPhoneTypes.GetCallPriority"/>, passing over the numbers that cannot be used.
    /// </summary>
    /// <remarks>
    /// A contact whose cell number is dead is still reachable at home. Passing over an excluded number keeps the same
    /// order of preference for the ones left, rather than giving up on the contact. A number whose type is not one
    /// of the known types is never called.
    /// </remarks>
    /// <param name="isExcluded">Whether a number must be passed over, or <see langword="null"/> to use every number.</param>
    public ContactPhoneNumber GetPreferredPhoneNumber(Func<string, bool> isExcluded = null)
    {
        ContactPhoneNumber preferred = null;
        var preferredPriority = int.MaxValue;

        foreach (var phoneNumber in PhoneNumbers)
        {
            if (phoneNumber is null ||
                string.IsNullOrEmpty(phoneNumber.PhoneNumber) ||
                ContactPhoneTypes.GetCallPriority(phoneNumber.Type) is not { } priority ||
                priority >= preferredPriority ||
                isExcluded?.Invoke(phoneNumber.PhoneNumber) == true)
            {
                continue;
            }

            preferred = phoneNumber;
            preferredPriority = priority;
        }

        return preferred;
    }
}
