using CrestApps.OrchardCore.Learning.Xapi.Models;

namespace CrestApps.OrchardCore.Learning.Xapi.Core.Services;

/// <summary>
/// The protected file area of statement attachments. No access policy is registered for it, so no signed link opens
/// an attachment; the statements resource serves them to a client that may read the statement.
/// </summary>
public sealed class XapiAttachmentFiles
{
}

/// <summary>
/// The content of an attachment sent in a <c>multipart/mixed</c> request.
/// </summary>
public sealed class XapiAttachmentContent
{
    /// <summary>Gets or sets the SHA-2 hash the part declared, in lower case hex.</summary>
    public string Sha2 { get; set; }

    /// <summary>Gets or sets the content type of the part.</summary>
    public string ContentType { get; set; }

    /// <summary>Gets or sets the content.</summary>
    public byte[] Content { get; set; } = [];
}

/// <summary>
/// What a statement write did.
/// </summary>
public sealed class XapiWriteResult
{
    /// <summary>Gets the HTTP status: 200 or 204 when stored, otherwise why not.</summary>
    public int Status { get; init; } = 200;

    /// <summary>Gets why the write was refused.</summary>
    public string Message { get; init; }

    /// <summary>Gets the ids of the statements, in the order they were sent.</summary>
    public IReadOnlyList<string> Ids { get; init; } = [];

    /// <summary>Gets the records stored now, without those the LRS already held.</summary>
    public IReadOnlyList<XapiStatementRecord> Stored { get; init; } = [];

    /// <summary>Gets whether the statements were stored.</summary>
    public bool Succeeded => Status is >= 200 and < 300;

    internal static XapiWriteResult Fail(int status, string message)
        => new() { Status = status, Message = message };
}

/// <summary>
/// The filters of a statement query, as xAPI 1.0.3 defines them.
/// </summary>
public sealed class XapiStatementQuery
{
    /// <summary>Gets or sets the agent key to match, or <see langword="null"/>.</summary>
    public string AgentKey { get; set; }

    /// <summary>Gets or sets whether the agent may be in any related position.</summary>
    public bool RelatedAgents { get; set; }

    /// <summary>Gets or sets the verb id, or <see langword="null"/>.</summary>
    public string Verb { get; set; }

    /// <summary>Gets or sets the activity id, or <see langword="null"/>.</summary>
    public string Activity { get; set; }

    /// <summary>Gets or sets whether the activity may be in the context or a sub-statement.</summary>
    public bool RelatedActivities { get; set; }

    /// <summary>Gets or sets the registration, or <see langword="null"/>.</summary>
    public string Registration { get; set; }

    /// <summary>Gets or sets the time after which statements were stored, in UTC.</summary>
    public DateTime? SinceUtc { get; set; }

    /// <summary>Gets or sets the time up to which statements were stored, in UTC.</summary>
    public DateTime? UntilUtc { get; set; }

    /// <summary>Gets or sets how many statements to return.</summary>
    public int Limit { get; set; }

    /// <summary>Gets or sets how many statements to skip, for a <c>more</c> link.</summary>
    public int Skip { get; set; }

    /// <summary>Gets or sets whether the oldest statements come first.</summary>
    public bool Ascending { get; set; }

    /// <summary>Gets or sets the last statement the result may hold, by the order statements were stored in, which the
    /// first page fixes so that the pages of a <c>more</c> link do not move.</summary>
    public long? MaxSequence { get; set; }
}

/// <summary>
/// A page of a statement query.
/// </summary>
public sealed class XapiQueryResult
{
    /// <summary>Gets the statements.</summary>
    public IReadOnlyList<XapiStatementRecord> Records { get; init; } = [];

    /// <summary>Gets whether more statements match.</summary>
    public bool HasMore { get; init; }
}
