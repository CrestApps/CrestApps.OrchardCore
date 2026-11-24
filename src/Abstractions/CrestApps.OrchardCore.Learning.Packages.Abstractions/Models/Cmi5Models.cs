using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Learning.Packages.Models;

/// <summary>
/// A cmi5 course structure, as read from the package's <c>cmi5.xml</c>: the course, its blocks and its assignable units
/// (AUs), in the order of the file.
/// </summary>
public sealed class Cmi5Course
{
    /// <summary>Gets or sets the course id the publisher gave (an IRI).</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the course title.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets the course description.</summary>
    public string Description { get; set; }

    /// <summary>Gets or sets the blocks, outer blocks first.</summary>
    public IList<Cmi5Block> Blocks { get; set; } = [];

    /// <summary>Gets or sets the assignable units, in the order of the file.</summary>
    public IList<Cmi5Au> Aus { get; set; } = [];
}

/// <summary>
/// A block of a cmi5 course.
/// </summary>
public sealed class Cmi5Block
{
    /// <summary>Gets or sets the block id the publisher gave.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets the id of the block it is in, or <see langword="null"/> for a block of the course.</summary>
    public string ParentId { get; set; }
}

/// <summary>
/// An assignable unit (AU) of a cmi5 course: content launched by the LMS, which reports to the LRS.
/// </summary>
public sealed class Cmi5Au
{
    /// <summary>Gets or sets the AU id the publisher gave (an IRI), which goes into the statements' grouping.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; }

    /// <summary>Gets or sets the URL as the course structure gives it.</summary>
    public string Url { get; set; }

    /// <summary>Gets or sets whether the URL is an absolute https URL rather than a file of the package.</summary>
    public bool IsAbsolute { get; set; }

    /// <summary>Gets or sets the path of the launched file in the package, for a relative URL.</summary>
    public string Path { get; set; }

    /// <summary>Gets or sets the query string of a relative URL, without the leading <c>?</c>.</summary>
    public string Query { get; set; }

    /// <summary>Gets or sets when the AU is satisfied: Passed, Completed, CompletedAndPassed, CompletedOrPassed or NotApplicable.</summary>
    public string MoveOn { get; set; } = "NotApplicable";

    /// <summary>Gets or sets the scaled score from 0 to 1 a learner must reach to pass, or <see langword="null"/>.</summary>
    public decimal? MasteryScore { get; set; }

    /// <summary>Gets or sets AnyWindow or OwnWindow.</summary>
    public string LaunchMethod { get; set; } = "AnyWindow";

    /// <summary>Gets or sets the launch parameters, which go into the launch data.</summary>
    public string LaunchParameters { get; set; }

    /// <summary>Gets or sets the entitlement key, which goes into the launch data.</summary>
    public string EntitlementKey { get; set; }

    /// <summary>Gets or sets the activity type the publisher gave.</summary>
    public string ActivityType { get; set; }

    /// <summary>Gets or sets the blocks the AU is in, outermost first.</summary>
    public IList<string> BlockIds { get; set; } = [];
}

/// <summary>
/// A learner's cmi5 state on a package under one enrollment: the registration, what each AU reported, which blocks and
/// whether the course are satisfied, and the launch sessions.
/// </summary>
public sealed class Cmi5State
{
    /// <summary>Gets or sets the registration of the enrollment on the course, a UUID in lower case.</summary>
    public string Registration { get; set; }

    /// <summary>Gets or sets the course id the registration is for.</summary>
    public string CourseId { get; set; }

    /// <summary>Gets or sets what each AU reported.</summary>
    public IList<Cmi5AuState> Aus { get; set; } = [];

    /// <summary>Gets or sets the blocks that are satisfied.</summary>
    public IList<string> SatisfiedBlocks { get; set; } = [];

    /// <summary>Gets or sets whether the course is satisfied.</summary>
    public bool CourseSatisfied { get; set; }

    /// <summary>Gets or sets the launch sessions: those still open and the last ones that ended.</summary>
    public IList<Cmi5Session> Sessions { get; set; } = [];
}

/// <summary>
/// What an AU reported in a registration.
/// </summary>
public sealed class Cmi5AuState
{
    /// <summary>Gets or sets the AU id the publisher gave.</summary>
    public string AuId { get; set; }

    /// <summary>Gets or sets whether the AU sent <c>completed</c>.</summary>
    public bool Completed { get; set; }

    /// <summary>Gets or sets whether the AU sent <c>passed</c>.</summary>
    public bool Passed { get; set; }

    /// <summary>Gets or sets whether the AU sent <c>failed</c>, and not <c>passed</c> since.</summary>
    public bool Failed { get; set; }

    /// <summary>Gets or sets the scaled score of the last <c>passed</c> or <c>failed</c>.</summary>
    public decimal? ScoreScaled { get; set; }

    /// <summary>Gets or sets whether the AU's moveOn is met, and the LMS wrote <c>satisfied</c>.</summary>
    public bool Satisfied { get; set; }

    /// <summary>Gets or sets when the AU was last launched, in UTC.</summary>
    public DateTime? LastLaunchedUtc { get; set; }
}

/// <summary>
/// A launch session of an AU.
/// </summary>
public sealed class Cmi5Session
{
    /// <summary>Gets or sets the session id, a UUID.</summary>
    public string SessionId { get; set; }

    /// <summary>Gets or sets the AU id the publisher gave.</summary>
    public string AuId { get; set; }

    /// <summary>Gets or sets the activity id the LMS gave the AU.</summary>
    public string ActivityId { get; set; }

    /// <summary>Gets or sets the launch mode: Normal, Browse or Review.</summary>
    public string LaunchMode { get; set; }

    /// <summary>Gets or sets when it was launched, in UTC.</summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>Gets or sets when its tokens expire, in UTC.</summary>
    public DateTime ExpiresUtc { get; set; }

    /// <summary>Gets or sets when the AU fetched its auth token, in UTC.</summary>
    public DateTime? FetchedUtc { get; set; }

    /// <summary>Gets or sets when the AU sent <c>initialized</c>, in UTC.</summary>
    public DateTime? InitializedUtc { get; set; }

    /// <summary>Gets or sets when the AU sent <c>terminated</c>, in UTC.</summary>
    public DateTime? TerminatedUtc { get; set; }

    /// <summary>Gets or sets when the LMS wrote <c>abandoned</c> for it, in UTC.</summary>
    public DateTime? AbandonedUtc { get; set; }

    /// <summary>Gets whether the session ended, by <c>terminated</c> or <c>abandoned</c>.</summary>
    [JsonIgnore]
    public bool Ended => TerminatedUtc is not null || AbandonedUtc is not null;
}
