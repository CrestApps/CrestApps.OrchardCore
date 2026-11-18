namespace CrestApps.OrchardCore.Learning.Credentials;

/// <summary>
/// The constants of Digital Credentials.
/// </summary>
public static class CredentialsConstants
{
    /// <summary>
    /// The YesSql collection of credential awards.
    /// </summary>
    public const string CollectionName = "LearningCredentials";

    /// <summary>
    /// The shared signing key set that signs credentials and status lists.
    /// </summary>
    public const string KeySet = "Learning.Credentials";

    /// <summary>
    /// The recipe and deployment step that carries credential awards as learner records.
    /// </summary>
    public const string RecipeStepName = "LearningCredentialAwards";

    /// <summary>
    /// The Orchard Core rate limit group of the public credential endpoints.
    /// </summary>
    public const string RateLimitGroup = "LearningCredentials";

    /// <summary>
    /// The notification type of a credential awarded to the person.
    /// </summary>
    public const string NotificationType = "LearningCredentialAwarded";

    /// <summary>
    /// The notification type of a credential of the person that was revoked.
    /// </summary>
    public const string RevokedNotificationType = "LearningCredentialRevoked";

    /// <summary>
    /// The number of entries in one status list: the Bitstring Status List minimum of 16 KB (131,072 bits), so a list
    /// says little about how many credentials the site issued.
    /// </summary>
    public const int StatusListCapacity = 131072;

    /// <summary>
    /// The feature ids.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Digital Credentials.
        /// </summary>
        public const string DigitalCredentials = "CrestApps.OrchardCore.Learning.Credentials";
    }

    /// <summary>
    /// The content types.
    /// </summary>
    public static class ContentTypes
    {
        /// <summary>
        /// The credential definition: what a credential says the holder achieved and when it is awarded.
        /// </summary>
        public const string CredentialDefinition = "CredentialDefinition";
    }

    /// <summary>
    /// The kinds of award rules. The kind is an open string; these are the ones the module registers.
    /// </summary>
    public static class RuleKinds
    {
        /// <summary>
        /// A course completed.
        /// </summary>
        public const string CourseCompleted = "CourseCompleted";

        /// <summary>
        /// A learning path completed.
        /// </summary>
        public const string LearningPathCompleted = "LearningPathCompleted";

        /// <summary>
        /// A certification issued.
        /// </summary>
        public const string CertificationIssued = "CertificationIssued";

        /// <summary>
        /// A quiz passed.
        /// </summary>
        public const string QuizPassed = "QuizPassed";

        /// <summary>
        /// Awarded by hand by a staff member.
        /// </summary>
        public const string Manual = "Manual";

        /// <summary>
        /// Awarded by the Award credential workflow task.
        /// </summary>
        public const string Workflow = "Workflow";
    }

    /// <summary>
    /// The names Open Badges 3.0 and the Verifiable Credentials Data Model 2.0 give to contexts, types and schemas.
    /// </summary>
    public static class OpenBadges
    {
        /// <summary>
        /// The Verifiable Credentials Data Model 2.0 context, always first.
        /// </summary>
        public const string VerifiableCredentialsContext = "https://www.w3.org/ns/credentials/v2";

        /// <summary>
        /// The Open Badges 3.0 context.
        /// </summary>
        public const string OpenBadgesContext = "https://purl.imsglobal.org/spec/ob/v3p0/context-3.0.3.json";

        /// <summary>
        /// The JSON schema of an Open Badges 3.0 achievement credential.
        /// </summary>
        public const string CredentialSchema = "https://purl.imsglobal.org/spec/ob/v3p0/schema/json/ob_v3p0_achievementcredential_schema.json";

        /// <summary>
        /// The type of the 1EdTech JSON schema validator.
        /// </summary>
        public const string CredentialSchemaType = "1EdTechJsonSchemaValidator2019";

        /// <summary>
        /// The status entry type of the W3C Bitstring Status List.
        /// </summary>
        public const string StatusEntryType = "BitstringStatusListEntry";

        /// <summary>
        /// The credential type of a Bitstring Status List.
        /// </summary>
        public const string StatusListCredentialType = "BitstringStatusListCredential";

        /// <summary>
        /// The subject type of a Bitstring Status List.
        /// </summary>
        public const string StatusListType = "BitstringStatusList";

        /// <summary>
        /// The status purpose of the list: revocation.
        /// </summary>
        public const string RevocationPurpose = "revocation";

        /// <summary>
        /// The media type of an unsecured credential in JSON-LD.
        /// </summary>
        public const string JsonLdMediaType = "application/ld+json";

        /// <summary>
        /// The media type of a credential signed as a JSON Web Token.
        /// </summary>
        public const string JwtMediaType = "application/jwt";
    }

    /// <summary>
    /// Gets the Open Badges 3.0 achievement types offered in the definition editor: the ones of the specification's
    /// enumeration that a learning site uses.
    /// </summary>
    public static IReadOnlyList<string> AchievementTypes { get; } =
    [
        "Badge",
        "Certificate",
        "CertificateOfCompletion",
        "Certification",
        "Course",
        "Competency",
        "LearningProgram",
        "MicroCredential",
        "Award",
        "Achievement",
        "License",
        "Membership",
    ];
}
