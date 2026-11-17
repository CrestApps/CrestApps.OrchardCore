using CrestApps.OrchardCore.Staff.Core.Handlers;

namespace CrestApps.OrchardCore.Tests.Staff;

/// <summary>
/// A staff member linked to a user takes the user's name, but a user without a name of their own only shows their user
/// name. Linking Avery Lee to the user "admin" renamed her "admin" in every pay run and profile.
/// </summary>
public sealed class StaffNameFromUserTests
{
    [Theory]
    [InlineData("Avery Lee", "avery", "A. Lee", "Avery Lee")]
    [InlineData("admin", "admin", "Avery Lee", "Avery Lee")]
    [InlineData("ADMIN ", "admin", "Avery Lee", "Avery Lee")]
    [InlineData(null, "admin", "Avery Lee", "Avery Lee")]
    [InlineData("admin", "admin", null, "admin")]
    [InlineData("  ", "admin", "", "admin")]
    public void ALinkedStaffMember_TakesTheUsersOwnName_NeverTheirUserNameOverARealName(string userDisplayName, string userName, string staffName, string expected)
        => Assert.Equal(expected, StaffMemberCatalogHandler.NameFromUser(userDisplayName, userName, staffName));
}
