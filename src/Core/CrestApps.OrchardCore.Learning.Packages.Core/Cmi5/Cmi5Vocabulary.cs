using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Cmi5;

/// <summary>
/// The IRIs and names of cmi5 (Quartz): verbs, context categories, extensions, the launch data state and the learner
/// preferences profile, and the activity ids the LMS gives a course, its blocks and its AUs.
/// </summary>
public static class Cmi5Vocabulary
{
    /// <summary>The kind of launch token cmi5 makes.</summary>
    public const string LaunchKind = "cmi5";

    /// <summary>The state id of the launch data.</summary>
    public const string LaunchDataStateId = "LMS.LaunchData";

    /// <summary>The agent profile id of the learner preferences.</summary>
    public const string LearnerPreferencesProfileId = "cmi5LearnerPreferences";

    /// <summary>The verbs of cmi5.</summary>
    public static class Verbs
    {
        /// <summary>Written by the LMS when it launches an AU.</summary>
        public const string Launched = "http://adlnet.gov/expapi/verbs/launched";

        /// <summary>The first statement of an AU session.</summary>
        public const string Initialized = "http://adlnet.gov/expapi/verbs/initialized";

        /// <summary>The learner completed the AU.</summary>
        public const string Completed = "http://adlnet.gov/expapi/verbs/completed";

        /// <summary>The learner passed the AU.</summary>
        public const string Passed = "http://adlnet.gov/expapi/verbs/passed";

        /// <summary>The learner failed the AU.</summary>
        public const string Failed = "http://adlnet.gov/expapi/verbs/failed";

        /// <summary>Written by the LMS when a session ended without <c>terminated</c>.</summary>
        public const string Abandoned = "https://w3id.org/xapi/adl/verbs/abandoned";

        /// <summary>Written by the LMS when it waives an AU.</summary>
        public const string Waived = "https://w3id.org/xapi/adl/verbs/waived";

        /// <summary>The last statement of an AU session.</summary>
        public const string Terminated = "http://adlnet.gov/expapi/verbs/terminated";

        /// <summary>Written by the LMS when the moveOn of an AU, a block or the course is met.</summary>
        public const string Satisfied = "https://w3id.org/xapi/adl/verbs/satisfied";
    }

    /// <summary>The context categories of cmi5.</summary>
    public static class Categories
    {
        /// <summary>Marks a cmi5 defined statement.</summary>
        public const string Cmi5 = "https://w3id.org/xapi/cmi5/context/categories/cmi5";

        /// <summary>Marks a statement that counts towards moveOn.</summary>
        public const string MoveOn = "https://w3id.org/xapi/cmi5/context/categories/moveon";
    }

    /// <summary>The extensions of cmi5.</summary>
    public static class Extensions
    {
        /// <summary>The session id, in the context.</summary>
        public const string SessionId = "https://w3id.org/xapi/cmi5/context/extensions/sessionid";

        /// <summary>The mastery score, in the context of passed and failed.</summary>
        public const string MasteryScore = "https://w3id.org/xapi/cmi5/context/extensions/masteryscore";

        /// <summary>The launch mode, in the context of launched.</summary>
        public const string LaunchMode = "https://w3id.org/xapi/cmi5/context/extensions/launchmode";

        /// <summary>The launch URL, in the context of launched.</summary>
        public const string LaunchUrl = "https://w3id.org/xapi/cmi5/context/extensions/launchurl";

        /// <summary>The moveOn, in the context of launched.</summary>
        public const string MoveOn = "https://w3id.org/xapi/cmi5/context/extensions/moveon";

        /// <summary>The launch parameters, in the context of launched.</summary>
        public const string LaunchParameters = "https://w3id.org/xapi/cmi5/context/extensions/launchparameters";
    }

    /// <summary>The verbs only the LMS may send in a cmi5 defined statement.</summary>
    public static readonly string[] LmsVerbs = [Verbs.Launched, Verbs.Abandoned, Verbs.Waived, Verbs.Satisfied];

    /// <summary>The verbs an AU may send in a cmi5 defined statement.</summary>
    public static readonly string[] AuVerbs = [Verbs.Initialized, Verbs.Completed, Verbs.Passed, Verbs.Failed, Verbs.Terminated];

    /// <summary>
    /// Gets the activity id the LMS gives an AU of a package item: unique to the item and the AU.
    /// </summary>
    /// <param name="homePage">The site's home page.</param>
    /// <param name="packageItemId">The package item.</param>
    /// <param name="auId">The AU id the publisher gave.</param>
    /// <returns>The activity id.</returns>
    public static string AuActivityId(string homePage, string packageItemId, string auId)
        => $"{homePage.TrimEnd('/')}/learning/cmi5/{packageItemId}/au/{ShortHash(auId)}";

    /// <summary>
    /// Gets the activity id the LMS gives a block of a package item.
    /// </summary>
    /// <param name="homePage">The site's home page.</param>
    /// <param name="packageItemId">The package item.</param>
    /// <param name="blockId">The block id the publisher gave.</param>
    /// <returns>The activity id.</returns>
    public static string BlockActivityId(string homePage, string packageItemId, string blockId)
        => $"{homePage.TrimEnd('/')}/learning/cmi5/{packageItemId}/block/{ShortHash(blockId)}";

    /// <summary>
    /// Gets the activity id the LMS gives the course of a package item.
    /// </summary>
    /// <param name="homePage">The site's home page.</param>
    /// <param name="packageItemId">The package item.</param>
    /// <returns>The activity id.</returns>
    public static string CourseActivityId(string homePage, string packageItemId)
        => $"{homePage.TrimEnd('/')}/learning/cmi5/{packageItemId}/course";

    private static string ShortHash(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)))[..16];
}
