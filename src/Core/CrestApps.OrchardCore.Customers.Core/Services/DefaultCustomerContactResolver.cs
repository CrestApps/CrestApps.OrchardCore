using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Customers.Core.Services;

/// <summary>
/// The default <see cref="ICustomerContactResolver"/>. An authenticated owner resolves through
/// <see cref="IUserService"/>; a guest owner resolves from the captured contact snapshot.
/// </summary>
public sealed class DefaultCustomerContactResolver : ICustomerContactResolver
{
    private readonly IUserService _userService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCustomerContactResolver"/> class.
    /// </summary>
    /// <param name="userService">The user service used to resolve authenticated owners.</param>
    public DefaultCustomerContactResolver(IUserService userService)
    {
        _userService = userService;
    }

    /// <inheritdoc/>
    public async Task<ICustomerContact> ResolveAsync(CustomerOwner owner, ICustomerContact guestContact, CancellationToken cancellationToken = default)
    {
        if (owner is null)
        {
            return null;
        }

        if (owner.Kind == CustomerOwnerKind.Guest)
        {
            return guestContact;
        }

        if (string.IsNullOrEmpty(owner.Id))
        {
            return null;
        }

        var user = await _userService.GetUserByUniqueIdAsync(owner.Id);

        if (user is null)
        {
            return null;
        }

        return new CustomerContact
        {
            DisplayName = user.UserName,
            Email = (user as User)?.Email,
        };
    }
}
