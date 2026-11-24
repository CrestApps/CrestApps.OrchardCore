namespace CrestApps.OrchardCore.Learning.Lti;

/// <summary>
/// The constants of External Tools: LTI 1.3 Core, Deep Linking 2.0, Names and Role Provisioning Services 2.0 and
/// Assignment and Grade Services 2.0, with Learning as the platform.
/// </summary>
public static class LtiConstants
{
    /// <summary>
    /// The YesSql collection of external tools, line items and scores.
    /// </summary>
    public const string CollectionName = "LearningLti";

    /// <summary>
    /// The course item kind of an external tool link.
    /// </summary>
    public const string ExternalToolKind = "ExternalTool";

    /// <summary>
    /// The name of the platform's key set in the shared signing keys.
    /// </summary>
    public const string PlatformKeySet = "Learning.Lti.Platform";

    /// <summary>
    /// The name of the recipe and deployment step of external tool settings.
    /// </summary>
    public const string RecipeStepName = "LearningExternalTools";

    /// <summary>
    /// The LTI version Learning launches with.
    /// </summary>
    public const string Version = "1.3.0";

    /// <summary>
    /// The feature ids.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// External Tools: LTI 1.3 with Deep Linking, Names and Role Provisioning and Assignment and Grade Services.
        /// </summary>
        public const string ExternalTools = "CrestApps.OrchardCore.Learning.Lti";
    }

    /// <summary>
    /// The content types.
    /// </summary>
    public static class ContentTypes
    {
        /// <summary>
        /// The External tool course item.
        /// </summary>
        public const string ExternalToolItem = "ExternalToolItem";
    }

    /// <summary>
    /// The LTI message types.
    /// </summary>
    public static class MessageTypes
    {
        /// <summary>
        /// A resource link launch.
        /// </summary>
        public const string ResourceLinkRequest = "LtiResourceLinkRequest";

        /// <summary>
        /// A Deep Linking request.
        /// </summary>
        public const string DeepLinkingRequest = "LtiDeepLinkingRequest";

        /// <summary>
        /// A Deep Linking response.
        /// </summary>
        public const string DeepLinkingResponse = "LtiDeepLinkingResponse";
    }

    /// <summary>
    /// The claims of the LTI messages.
    /// </summary>
    public static class Claims
    {
        /// <summary>
        /// The message type.
        /// </summary>
        public const string MessageType = "https://purl.imsglobal.org/spec/lti/claim/message_type";

        /// <summary>
        /// The LTI version.
        /// </summary>
        public const string Version = "https://purl.imsglobal.org/spec/lti/claim/version";

        /// <summary>
        /// The deployment id.
        /// </summary>
        public const string DeploymentId = "https://purl.imsglobal.org/spec/lti/claim/deployment_id";

        /// <summary>
        /// The target link URI.
        /// </summary>
        public const string TargetLinkUri = "https://purl.imsglobal.org/spec/lti/claim/target_link_uri";

        /// <summary>
        /// The resource link.
        /// </summary>
        public const string ResourceLink = "https://purl.imsglobal.org/spec/lti/claim/resource_link";

        /// <summary>
        /// The roles.
        /// </summary>
        public const string Roles = "https://purl.imsglobal.org/spec/lti/claim/roles";

        /// <summary>
        /// The context (course).
        /// </summary>
        public const string Context = "https://purl.imsglobal.org/spec/lti/claim/context";

        /// <summary>
        /// The platform instance.
        /// </summary>
        public const string ToolPlatform = "https://purl.imsglobal.org/spec/lti/claim/tool_platform";

        /// <summary>
        /// The launch presentation.
        /// </summary>
        public const string LaunchPresentation = "https://purl.imsglobal.org/spec/lti/claim/launch_presentation";

        /// <summary>
        /// The custom parameters.
        /// </summary>
        public const string Custom = "https://purl.imsglobal.org/spec/lti/claim/custom";

        /// <summary>
        /// The Assignment and Grade Services endpoint.
        /// </summary>
        public const string AgsEndpoint = "https://purl.imsglobal.org/spec/lti-ags/claim/endpoint";

        /// <summary>
        /// The Names and Role Provisioning Services endpoint.
        /// </summary>
        public const string NrpsEndpoint = "https://purl.imsglobal.org/spec/lti-nrps/claim/namesroleservice";

        /// <summary>
        /// The Deep Linking settings of a Deep Linking request.
        /// </summary>
        public const string DeepLinkingSettings = "https://purl.imsglobal.org/spec/lti-dl/claim/deep_linking_settings";

        /// <summary>
        /// The content items of a Deep Linking response.
        /// </summary>
        public const string ContentItems = "https://purl.imsglobal.org/spec/lti-dl/claim/content_items";

        /// <summary>
        /// The data a Deep Linking response echoes.
        /// </summary>
        public const string Data = "https://purl.imsglobal.org/spec/lti-dl/claim/data";

        /// <summary>
        /// The message a Deep Linking response shows the user.
        /// </summary>
        public const string Msg = "https://purl.imsglobal.org/spec/lti-dl/claim/msg";

        /// <summary>
        /// The error message a Deep Linking response shows the user.
        /// </summary>
        public const string ErrorMsg = "https://purl.imsglobal.org/spec/lti-dl/claim/errormsg";
    }

    /// <summary>
    /// The OAuth 2 scopes of the services, registered as OpenID scopes.
    /// </summary>
    public static class Scopes
    {
        /// <summary>
        /// Read and write line items.
        /// </summary>
        public const string LineItem = "https://purl.imsglobal.org/spec/lti-ags/scope/lineitem";

        /// <summary>
        /// Read line items.
        /// </summary>
        public const string LineItemReadOnly = "https://purl.imsglobal.org/spec/lti-ags/scope/lineitem.readonly";

        /// <summary>
        /// Read results.
        /// </summary>
        public const string ResultReadOnly = "https://purl.imsglobal.org/spec/lti-ags/scope/result.readonly";

        /// <summary>
        /// Post scores.
        /// </summary>
        public const string Score = "https://purl.imsglobal.org/spec/lti-ags/scope/score";

        /// <summary>
        /// Read the memberships of a context.
        /// </summary>
        public const string ContextMembershipReadOnly = "https://purl.imsglobal.org/spec/lti-nrps/scope/contextmembership.readonly";

        /// <summary>
        /// Gets every scope of the services.
        /// </summary>
        public static IReadOnlyList<string> All { get; } = [LineItem, LineItemReadOnly, ResultReadOnly, Score, ContextMembershipReadOnly];
    }

    /// <summary>
    /// The LIS role vocabulary.
    /// </summary>
    public static class Roles
    {
        /// <summary>
        /// A learner of the context.
        /// </summary>
        public const string Learner = "http://purl.imsglobal.org/vocab/lis/v2/membership#Learner";

        /// <summary>
        /// An instructor of the context.
        /// </summary>
        public const string Instructor = "http://purl.imsglobal.org/vocab/lis/v2/membership#Instructor";

        /// <summary>
        /// A teaching assistant, a sub-role of instructor.
        /// </summary>
        public const string TeachingAssistant = "http://purl.imsglobal.org/vocab/lis/v2/membership/Instructor#TeachingAssistant";

        /// <summary>
        /// A grader, a sub-role of instructor.
        /// </summary>
        public const string Grader = "http://purl.imsglobal.org/vocab/lis/v2/membership/Instructor#Grader";

        /// <summary>
        /// A mentor of the context.
        /// </summary>
        public const string Mentor = "http://purl.imsglobal.org/vocab/lis/v2/membership#Mentor";

        /// <summary>
        /// An administrator of the context.
        /// </summary>
        public const string ContextAdministrator = "http://purl.imsglobal.org/vocab/lis/v2/membership#Administrator";

        /// <summary>
        /// A content developer of the context.
        /// </summary>
        public const string ContentDeveloper = "http://purl.imsglobal.org/vocab/lis/v2/membership#ContentDeveloper";
    }

    /// <summary>
    /// The media types of the services.
    /// </summary>
    public static class MediaTypes
    {
        /// <summary>
        /// A line item.
        /// </summary>
        public const string LineItem = "application/vnd.ims.lis.v2.lineitem+json";

        /// <summary>
        /// A list of line items.
        /// </summary>
        public const string LineItemContainer = "application/vnd.ims.lis.v2.lineitemcontainer+json";

        /// <summary>
        /// A score.
        /// </summary>
        public const string Score = "application/vnd.ims.lis.v1.score+json";

        /// <summary>
        /// A list of results.
        /// </summary>
        public const string ResultContainer = "application/vnd.ims.lis.v2.resultcontainer+json";

        /// <summary>
        /// A list of memberships.
        /// </summary>
        public const string MembershipContainer = "application/vnd.ims.lti-nrps.v2.membershipcontainer+json";
    }
}
