namespace CrestApps.OrchardCore.MailingLists;

/// <summary>
/// Names shared by the mailing lists building block and the modules that extend it.
/// </summary>
public static class MailingListsConstants
{
    /// <summary>
    /// The YesSql collection that holds mailing lists, members and the provider outbox.
    /// </summary>
    public const string CollectionName = "MailingLists";

    /// <summary>
    /// The site settings group of the mailing lists settings page (e-mail templates, provider connections).
    /// </summary>
    public const string SettingsGroupId = "mailing-lists";

    /// <summary>
    /// The name of the built-in provider that keeps the list on the site.
    /// </summary>
    public const string LocalProviderName = "Local";

    /// <summary>
    /// The name the mailing lists consent source records changes under, so it can ignore its own changes.
    /// </summary>
    public const string ConsentSourceName = "mailing-lists";

    /// <summary>
    /// The unsubscribe scope kind of a link that unsubscribes one mailing list (the scope id is the list id).
    /// </summary>
    public const string UnsubscribeScopeKind = "MailingList";

    /// <summary>
    /// The record kind of a mailing list member, for record links, timelines and coupon sources.
    /// </summary>
    public const string MemberRecordKind = "MailingListMember";

    /// <summary>
    /// The data protection purpose of verification links.
    /// </summary>
    public const string VerifyTokenPurpose = "CrestApps.MailingLists.Verify";

    /// <summary>
    /// The name of the Outreach audience source that reads the subscribed members of one list.
    /// </summary>
    public const string OutreachAudienceSource = "MailingList";

    /// <summary>
    /// The property of an Outreach run that holds the id of the mailing list the run sends to. When it is missing, the
    /// run's owner id is read as the list id if the run's owner type is <see cref="OutreachAudienceSource"/>.
    /// </summary>
    public const string OutreachListIdProperty = "MailingListId";

    /// <summary>
    /// The templated e-mail group of the mailing lists e-mails.
    /// </summary>
    public const string EmailGroup = "MailingLists";

    /// <summary>
    /// The names of the public routes of the mailing lists module.
    /// </summary>
    public static class RouteNames
    {
        /// <summary>
        /// The page a confirmation or claim link opens (<c>mailing-lists/verify?token=</c>; the token rides in the query string, since IIS refuses a path segment over 260 characters).
        /// </summary>
        public const string Verify = "MailingListVerify";

        /// <summary>
        /// The post that sends a new link for an expired one (<c>mailing-lists/verify/resend?token=</c>).
        /// </summary>
        public const string VerifyResend = "MailingListVerifyResend";

        /// <summary>
        /// The post of a sign-up widget (<c>mailing-lists/subscribe/{widgetId}</c>).
        /// </summary>
        public const string Subscribe = "MailingListSubscribe";
    }

    /// <summary>
    /// The feature ids of the mailing lists module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Mailing lists, members, verification and the provider seam.
        /// </summary>
        public const string MailingLists = "CrestApps.OrchardCore.MailingLists";

        /// <summary>
        /// The newsletter sign-up widget.
        /// </summary>
        public const string SignupWidget = "CrestApps.OrchardCore.MailingLists.SignupWidget";

        /// <summary>
        /// The welcome coupon a list can give its new members (needs Checkout).
        /// </summary>
        public const string WelcomeCoupon = "CrestApps.OrchardCore.MailingLists.WelcomeCoupon";
    }

    /// <summary>
    /// The templated e-mail kinds of the mailing lists e-mails.
    /// </summary>
    public static class EmailKinds
    {
        /// <summary>
        /// The e-mail with the link that confirms a sign-up (double opt-in).
        /// </summary>
        public const string Confirm = "mailing-list-confirm";

        /// <summary>
        /// The e-mail sent when a person is subscribed; carries the welcome coupon when one was issued.
        /// </summary>
        public const string Welcome = "mailing-list-welcome";

        /// <summary>
        /// The e-mail with the link that verifies the address and claims the welcome coupon, for lists without
        /// confirmation whose coupon waits for verification.
        /// </summary>
        public const string CouponClaim = "mailing-list-coupon-claim";
    }

    /// <summary>
    /// The Orchard Core Rate Limits groups of the public mailing lists endpoints.
    /// </summary>
    public static class RateLimitGroups
    {
        /// <summary>
        /// The public sign-up form.
        /// </summary>
        public const string Signup = "MailingListSignup";
    }
}
