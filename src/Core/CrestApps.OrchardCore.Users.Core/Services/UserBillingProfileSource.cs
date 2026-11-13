using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;

namespace CrestApps.OrchardCore.Users.Core.Services;

/// <summary>
/// Bills a user (party kind <see cref="PartyKinds.User"/>) under their display name from the
/// <see cref="IDisplayNameProvider"/>, at their e-mail address, with the phone number kept on the user when it is in
/// E.164 form. A user keeps no postal address.
/// </summary>
public sealed class UserBillingProfileSource : IPartyBillingProfileSource
{
    private readonly IUserSearchService _userSearchService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserBillingProfileSource"/> class.
    /// </summary>
    /// <param name="userSearchService">The user search service.</param>
    public UserBillingProfileSource(IUserSearchService userSearchService)
    {
        _userSearchService = userSearchService;
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
            user.PhoneNumber);

        var contact = profile.Contact ?? new ContactInfo();

        profile.Contact = contact with
        {
            Name = found.Name ?? contact.Name,
            Email = found.Email ?? contact.Email,
            Phone = found.Phone.HasValue ? found.Phone : contact.Phone,
        };
    }
}
