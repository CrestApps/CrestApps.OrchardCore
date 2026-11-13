using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.PhoneNumbers;

namespace CrestApps.OrchardCore.Users.Core.Services;

/// <summary>
/// Bills a user (party kind <see cref="PartyKinds.User"/>) under their display name from the
/// <see cref="IDisplayNameProvider"/>, at their e-mail address, with the phone number kept on the user in E.164 form,
/// normalized by the phone number service when it was typed another way. A user keeps no postal address.
/// </summary>
public sealed class UserBillingProfileSource : IPartyBillingProfileSource
{
    private readonly IUserSearchService _userSearchService;
    private readonly IPhoneNumberService _phoneNumberService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserBillingProfileSource"/> class.
    /// </summary>
    /// <param name="userSearchService">The user search service.</param>
    /// <param name="phoneNumberService">The phone number service that turns a phone typed in another form, such as with spaces, into E.164; <see langword="null"/> when the Phone Numbers feature is off.</param>
    public UserBillingProfileSource(IUserSearchService userSearchService, IPhoneNumberService phoneNumberService = null)
    {
        _userSearchService = userSearchService;
        _phoneNumberService = phoneNumberService;
    }

    /// <inheritdoc/>
    public string Kind => PartyKinds.User;

    /// <inheritdoc/>
    public async Task ContributeAsync(PartyRef party, PartyBillingProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (party.IsEmpty)
        {
            return;
        }

        var users = await _userSearchService.GetAsync([party.Id], cancellationToken);

        if (users is not { Count: > 0 } || users[0] is not { } user)
        {
            return;
        }

        var found = ContactInfo.From(
            string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName,
            user.Email,
            PhoneNumberNormalization.ToE164OrTrimmed(user.PhoneNumber, _phoneNumberService));

        var contact = profile.Contact ?? new ContactInfo();

        profile.Contact = contact with
        {
            Name = found.Name ?? contact.Name,
            Email = found.Email ?? contact.Email,
            Phone = found.Phone.HasValue ? found.Phone : contact.Phone,
        };
    }
}
