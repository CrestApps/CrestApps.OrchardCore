namespace CrestApps.OrchardCore.Learning.Xapi;

/// <summary>
/// The names the Learning Record Store uses.
/// </summary>
public static class XapiConstants
{
    /// <summary>
    /// The xAPI version the LRS implements and sends in the <c>X-Experience-API-Version</c> header of every response.
    /// </summary>
    public const string Version = "1.0.3";

    /// <summary>
    /// The version the LRS writes into a statement that has none, as xAPI 1.0.3 asks.
    /// </summary>
    public const string DefaultStatementVersion = "1.0.0";

    /// <summary>
    /// The path of the LRS endpoint, under the tenant's path. Resources follow it: <c>statements</c>, <c>activities</c>,
    /// <c>activities/state</c>, <c>activities/profile</c>, <c>agents</c>, <c>agents/profile</c> and <c>about</c>.
    /// </summary>
    public const string RoutePrefix = "learning/xapi";

    /// <summary>
    /// The YesSql collection of statements, documents and activity definitions.
    /// </summary>
    public const string CollectionName = "LearningXapi";

    /// <summary>
    /// The name of the statements recipe and deployment step.
    /// </summary>
    public const string StatementsRecipeStep = "LearningStatements";

    /// <summary>
    /// The name of the data source of Data Pipelines and the Report Builder.
    /// </summary>
    public const string DataSourceName = "Learning.Xapi";

    /// <summary>
    /// The rate limit group of the LRS endpoint, applied when Orchard Core's Rate Limits feature is on.
    /// </summary>
    public const string RateLimitGroup = "LearningXapi";

    /// <summary>
    /// The folder of the protected file area of statement attachments, in the tenant's App_Data.
    /// </summary>
    public const string AttachmentsFolder = "LearningXapiAttachments";

    /// <summary>
    /// The features of the Learning Record Store.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Learning Record Store: the xAPI statements, documents, activities, agents and about resources.
        /// </summary>
        public const string LearningRecordStore = "CrestApps.OrchardCore.Learning.Xapi";
    }

    /// <summary>
    /// The HTTP headers xAPI defines.
    /// </summary>
    public static class Headers
    {
        /// <summary>The version header every request and response carries.</summary>
        public const string Version = "X-Experience-API-Version";

        /// <summary>The time up to which a statement query is known to be complete.</summary>
        public const string ConsistentThrough = "X-Experience-API-Consistent-Through";

        /// <summary>The SHA-2 hash of an attachment part.</summary>
        public const string Hash = "X-Experience-API-Hash";
    }

    /// <summary>
    /// The verbs the LRS itself acts on.
    /// </summary>
    public static class Verbs
    {
        /// <summary>The verb of a voiding statement.</summary>
        public const string Voided = "http://adlnet.gov/expapi/verbs/voided";
    }

    /// <summary>
    /// The attachment usage types the LRS checks.
    /// </summary>
    public static class AttachmentUsages
    {
        /// <summary>The JSON web signature of a signed statement.</summary>
        public const string Signature = "http://adlnet.gov/expapi/attachments/signature";
    }
}
