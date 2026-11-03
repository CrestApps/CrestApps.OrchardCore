namespace CrestApps.OrchardCore.NLWeb;

/// <summary>
/// Provides the constants of the NLWeb module.
/// </summary>
public static class NLWebConstants
{
    /// <summary>
    /// The route of the <c>ask</c> endpoint, at the root of the tenant where NLWeb clients look for it.
    /// </summary>
    public const string AskRoute = "ask";

    /// <summary>
    /// The route of the <c>await</c> endpoint.
    /// </summary>
    public const string AwaitRoute = "await";

    /// <summary>
    /// The settings group of the NLWeb site settings.
    /// </summary>
    public const string SettingsGroupId = "nlweb";

    /// <summary>
    /// The rate limit group of the NLWeb endpoints, used by Orchard Core's Rate Limits feature.
    /// </summary>
    public const string RateLimitGroup = "NLWeb";

    /// <summary>
    /// The most requests a visitor can send to the NLWeb endpoints in a minute when Orchard Core's Rate Limits
    /// feature is enabled.
    /// </summary>
    public const int RequestsPerMinute = 30;

    /// <summary>
    /// The largest request body accepted, in bytes.
    /// </summary>
    public const int MaxRequestBodySize = 64 * 1024;

    /// <summary>
    /// The most results an answer can have.
    /// </summary>
    public const int MaxResultsLimit = 50;

    /// <summary>
    /// The name of the AI tool that asks the site a question.
    /// </summary>
    public const string AskToolName = "ask";

    /// <summary>
    /// The feature identifiers.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The NLWeb feature.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.NLWeb";
    }

    /// <summary>
    /// The names of the retrievers.
    /// </summary>
    public static class Retrievers
    {
        /// <summary>
        /// The keyword retriever over Orchard Core's search indexes.
        /// </summary>
        public const string Index = "index";

        /// <summary>
        /// The vector retriever over an AI data source.
        /// </summary>
        public const string Vector = "vector";
    }
}
