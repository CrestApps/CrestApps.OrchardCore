namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Contains constant values for users.
/// </summary>
public static class UsersConstants
{
    /// <summary>
    /// The cache tag of the user display names, removed when a user's name changes.
    /// </summary>
    public const string UserDisplayNameCacheTag = "user-display-name";

    /// <summary>
    /// Represents the feature.
    /// </summary>
    public static class Feature
    {
        public const string Area = "CrestApps.OrchardCore.Users";

        public const string DisplayName = "CrestApps.OrchardCore.Users.DisplayName";

        public const string EmailCodeSignIn = "CrestApps.OrchardCore.Users.EmailCodeSignIn";

        public const string EmailOwnership = "CrestApps.OrchardCore.Users.EmailOwnership";

        public const string Avatars = "CrestApps.OrchardCore.Users.Avatars";

        public const string Audiences = "CrestApps.OrchardCore.Users.Audiences";

        /// <summary>
        /// Cohorts: named groups of people kept by hand, and the "Cohort member" audience rule.
        /// </summary>
        public const string Cohorts = "CrestApps.OrchardCore.Users.Cohorts";

        /// <summary>
        /// The "my account" area on the site: <c>/account</c>, its menu and the frame of its pages, which modules add
        /// their pages to (orders, tickets, subscriptions...).
        /// </summary>
        public const string Account = "CrestApps.OrchardCore.Users.Account";
    }

    /// <summary>
    /// The "my account" area. The names predate the area's move from Commerce to Users and are kept, so themes and
    /// modules that use them keep working.
    /// </summary>
    public static class Account
    {
        /// <summary>
        /// The name of the navigation menu that lists the account's pages. A module adds a page with a
        /// <c>NamedNavigationProvider</c> for this menu.
        /// </summary>
        public const string MenuName = "commerce-account";

        /// <summary>
        /// The route name of the account's home page, at <c>/account</c>.
        /// </summary>
        public const string HomeRouteName = "CommerceAccount";

        /// <summary>
        /// The shape that renders the account menu beside an account page.
        /// </summary>
        public const string NavigationShape = "CommerceAccountNavigation";

        /// <summary>
        /// The shape that frames an account page: the account menu beside the page's heading and content. A page renders
        /// it with its <c>Title</c>, its <c>Content</c> (a shape or HTML) and, optionally, <c>Actions</c> beside the
        /// heading, and keeps its own breadcrumb. A theme overrides it to change every account page.
        /// </summary>
        public const string PageShape = "CommerceAccountPage";
    }

    /// <summary>
    /// The site's Notifications page, where a signed-in user reads their Orchard Core notifications without the admin.
    /// It is on when Orchard Core's Notifications feature is on.
    /// </summary>
    public static class Notifications
    {
        /// <summary>
        /// The route name of the page, at <c>/notifications</c>.
        /// </summary>
        public const string RouteName = "UserNotifications";

        /// <summary>
        /// The route name of the form that marks all the user's notifications read.
        /// </summary>
        public const string MarkAllReadRouteName = "UserNotificationsMarkAllRead";

        /// <summary>
        /// The route name of the page where a signed-in user chooses how each kind of notification reaches them, at
        /// <c>/notifications/preferences</c>: the Notifications module's own preferences, without the admin.
        /// </summary>
        public const string PreferencesRouteName = "UserNotificationPreferences";
    }

    /// <summary>
    /// The name of the recipe and deployment step of the audiences administrators define.
    /// </summary>
    public const string AudiencesRecipeStepName = "UserAudiences";

    /// <summary>
    /// The name of the recipe and deployment step of cohorts and their members.
    /// </summary>
    public const string CohortsRecipeStepName = "UserCohorts";

    /// <summary>
    /// The YesSql collection of cohorts and their members.
    /// </summary>
    public const string CohortsCollectionName = "Cohorts";
}
