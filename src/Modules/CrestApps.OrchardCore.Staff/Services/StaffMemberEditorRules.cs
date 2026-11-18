using CrestApps.OrchardCore.Staff.Models;

namespace CrestApps.OrchardCore.Staff.Services;

/// <summary>
/// The staff editor's own rules for the core fields, shared by its driver and the AI tools: the user account is linked
/// once, a linked person takes their name and e-mail from their account, a new home base address drops the old
/// coordinates, and only the capabilities offered are changed.
/// </summary>
internal static class StaffMemberEditorRules
{
    /// <summary>
    /// Links a user account to a staff member that has none. A staff member keeps the account it was linked to: pay,
    /// history and badges never move to another person.
    /// </summary>
    /// <param name="staff">The staff member.</param>
    /// <param name="userId">The user, or empty for none.</param>
    public static void LinkUser(StaffMember staff, string userId)
    {
        if (string.IsNullOrEmpty(staff.UserId))
        {
            staff.UserId = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
        }
    }

    /// <summary>
    /// Sets the name and e-mail of a person without a user account; a person with one takes them from it when saved.
    /// </summary>
    /// <param name="staff">The staff member.</param>
    /// <param name="displayName">The name.</param>
    /// <param name="email">The e-mail.</param>
    public static void SetNameAndEmail(StaffMember staff, string displayName, string email)
    {
        if (string.IsNullOrEmpty(staff.UserId))
        {
            staff.DisplayName = displayName;
            staff.Email = email;
        }
    }

    /// <summary>
    /// Sets where the person starts their day. A new address drops the old coordinates, so they are found again.
    /// </summary>
    /// <param name="staff">The staff member.</param>
    /// <param name="address">The address, or empty for none.</param>
    public static void SetHomeBase(StaffMember staff, string address)
    {
        var homeBaseMoved = !string.Equals(staff.HomeBase?.Address, address?.Trim(), StringComparison.Ordinal);

        staff.HomeBase = string.IsNullOrWhiteSpace(address) ? null : new StaffLocation
        {
            Address = address.Trim(),
            Latitude = homeBaseMoved ? null : staff.HomeBase?.Latitude,
            Longitude = homeBaseMoved ? null : staff.HomeBase?.Longitude,
        };
    }

    /// <summary>
    /// Replaces the offered capabilities a person has with the chosen ones. Ones turned off, or that a module set and
    /// doesn't offer, stay.
    /// </summary>
    /// <param name="staff">The staff member.</param>
    /// <param name="offered">The names of the capabilities offered.</param>
    /// <param name="chosen">The names chosen.</param>
    public static void SetCapabilities(StaffMember staff, IReadOnlySet<string> offered, IEnumerable<string> chosen)
    {
        staff.Capabilities = staff.Capabilities
            .Where(capability => !offered.Contains(capability))
            .Concat((chosen ?? []).Where(offered.Contains))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
