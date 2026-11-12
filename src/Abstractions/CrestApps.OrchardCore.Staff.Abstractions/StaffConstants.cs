namespace CrestApps.OrchardCore.Staff;

/// <summary>
/// Names shared by the Staff module and the modules that build on it.
/// </summary>
public static class StaffConstants
{
    /// <summary>
    /// The YesSql collection that holds staff members and their history.
    /// </summary>
    public const string CollectionName = "Staff";

    /// <summary>
    /// The name of the recipe step, and of the deployment step, that carries staff members.
    /// </summary>
    public const string RecipeStepName = "Staff";

    /// <summary>
    /// The party kind of a staff member in <c>PartyRef</c> values.
    /// </summary>
    public const string PartyKind = "Staff";

    /// <summary>
    /// The default name of the role every active staff member with a user account is put in.
    /// </summary>
    public const string DefaultStaffRoleName = "Staff";

    /// <summary>
    /// The user audience of working staff members, for user pickers and searches (<c>UserSearchQuery.Audience</c>) that
    /// should only offer staff. When Staff is off, pickers asking for it offer every user.
    /// </summary>
    public const string UserAudience = "Staff";

    /// <summary>
    /// The user audience of the accounts that can still be linked to a staff member: every user who isn't linked to
    /// one yet. The staff editor's user picker asks for it, since a user account belongs to one staff member only.
    /// </summary>
    public const string UnlinkedUserAudience = "StaffUnlinked";

    /// <summary>
    /// The source recorded when Staff grants its role, for <c>IRoleGrantService</c>.
    /// </summary>
    public const string RoleGrantSource = "Staff";

    /// <summary>
    /// The source recorded on team memberships Staff supplies to Teams.
    /// </summary>
    public const string TeamMemberSource = "Staff";

    /// <summary>
    /// The feature identifiers of the Staff module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Staff feature: the staff record, list, editor, directory and hierarchy.
        /// </summary>
        public const string Staff = "CrestApps.OrchardCore.Staff";

        /// <summary>
        /// Dated pay rates and cost rates on staff members.
        /// </summary>
        public const string Compensation = "CrestApps.OrchardCore.Staff.Compensation";

        /// <summary>
        /// Badge numbers and PINs, for kiosks and quick sign-in at a register.
        /// </summary>
        public const string Badges = "CrestApps.OrchardCore.Staff.Badges";

        /// <summary>
        /// The organization chart.
        /// </summary>
        public const string OrgChart = "CrestApps.OrchardCore.Staff.OrgChart";
    }
}
