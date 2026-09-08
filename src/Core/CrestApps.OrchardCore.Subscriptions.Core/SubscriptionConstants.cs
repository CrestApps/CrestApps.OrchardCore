namespace CrestApps.OrchardCore.Subscriptions.Core;

/// <summary>
/// Defines shared constants used by the subscriptions module.
/// </summary>
public static class SubscriptionConstants
{
    /// <summary>
    /// The content type stereotype used to identify subscription content types.
    /// </summary>
    public const string Stereotype = "Subscription";

    /// <summary>
    /// The YesSql collection the durable subscription agreements are stored in. Keeping them in their own
    /// collection means a subscription report never scans the tenant's whole document table.
    /// </summary>
    public const string SubscriptionCollectionName = "Subscription";

    /// <summary>
    /// The YesSql collection the durable tenant provisioning jobs are stored in.
    /// </summary>
    public const string TenantProvisioningCollectionName = "TenantProvisioning";

    /// <summary>
    /// The entitlement kinds shipped with the module. A kind selects the feature that applies what a
    /// subscription grants, so the subscription module never has to know what a role or a tenant is.
    /// </summary>
    public static class EntitlementKinds
    {
        /// <summary>
        /// Grants an Orchard Core role for as long as the subscription is current. Roles already gate
        /// content, features, and permissions, so this makes all of those subscription-aware at once.
        /// </summary>
        public const string Role = "Role";

        /// <summary>
        /// Grants access to a provisioned Orchard Core tenant for as long as the subscription is current.
        /// </summary>
        public const string Tenant = "Tenant";
    }

    /// <summary>
    /// The content type name for the subscription summary dashboard widget.
    /// </summary>
    public const string SubscriptionSummaryWidgetType = "SubscriptionSummaryWidget";



    /// <summary>
    /// The prefix used to identify initial-fee payment entries.
    /// </summary>
    public const string InitialFeeIdPrefix = "__InitialFee";

    /// <summary>
    /// Defines named routes used by subscription payment and checkout endpoints.
    /// </summary>
    public static class RouteName
    {
        /// <summary>
        /// The route that starts a checkout for a published subscription plan.
        /// </summary>
        public const string Signup = "SubscriptionSignup";
    }

    /// <summary>
    /// Defines feature identifiers for subscription module features.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Orchard Core area name for the subscriptions module.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.Subscriptions";



        /// <summary>
        /// The feature identifier for selling Orchard Core tenants through the public checkout.
        /// </summary>
        public const string Tenants = "CrestApps.OrchardCore.Subscriptions.Tenants";
    }

    /// <summary>
    /// Defines keys for built-in subscription checkout flow steps.
    /// </summary>
    public static class StepKey
    {
        /// <summary>
        /// The step key for user registration.
        /// </summary>
        public const string UserRegistration = "UserRegistration";


        /// <summary>
        /// The checkout step key used to capture the details of a site bought through the public checkout.
        /// </summary>
        public const string TenantProvisioning = "TenantProvisioning";

    }

    /// <summary>
    /// Defines the data-protection purposes used by the subscription flow. A purpose is shared between the
    /// component that protects a secret and the component that consumes it, so it lives here rather than on
    /// either side: protecting with one purpose and unprotecting with another silently fails.
    /// </summary>
    public static class ProtectorPurposes
    {
        /// <summary>
        /// The purpose used to protect the tenant administrator password collected by the tenant onboarding
        /// step and consumed by the tenant provisioning handler.
        /// </summary>
        public const string TenantOnboardingStep = "TenantOnboardingStep";

        /// <summary>
        /// The data-protection purpose used for the password captured on the account step.
        /// </summary>
        public const string UserRegistrationStep = "UserRegistrationStep";
    }
}
