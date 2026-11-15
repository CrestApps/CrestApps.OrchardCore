using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Users;

/// <summary>
/// The "my account" area moved from Commerce to Users with its names unchanged: the Commerce constants still name the
/// same menu, route and shapes, so themes and modules that use either keep working.
/// </summary>
public sealed class AccountAreaConstantsTests
{
    [Fact]
    public void TheCommerceNames_StayTheUsersNames()
    {
        Assert.Equal(UsersConstants.Account.MenuName, CommerceConstants.Account.MenuName);
        Assert.Equal(UsersConstants.Account.HomeRouteName, CommerceConstants.Account.HomeRouteName);
        Assert.Equal(UsersConstants.Account.NavigationShape, CommerceConstants.Account.NavigationShape);
        Assert.Equal(UsersConstants.Account.PageShape, CommerceConstants.Account.PageShape);
    }
}
