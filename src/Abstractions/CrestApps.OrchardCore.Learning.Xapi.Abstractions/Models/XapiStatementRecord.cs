using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Xapi.Models;

/// <summary>
/// A statement in the Learning Record Store: the statement as it was stored (with the <c>id</c>, <c>stored</c>,
/// <c>authority</c>, <c>version</c> and <c>timestamp</c> the LRS set), whether it was voided, and the keys the
/// statement queries filter on. The statement itself never changes once stored; only <see cref="Voided"/> and the
/// keys inherited through a statement reference do.
/// </summary>
public sealed class XapiStatementRecord : CatalogItem
{
    /// <summary>Gets or sets the statement id, a UUID in lower case.</summary>
    public string StatementId { get; set; }

    /// <summary>Gets or sets the statement as stored, as JSON.</summary>
    public string Statement { get; set; }

    /// <summary>Gets or sets the position of the statement in the order the LRS stored statements in.</summary>
    public long Sequence { get; set; }

    /// <summary>Gets or sets when the LRS stored the statement, in UTC, to the millisecond.</summary>
    public DateTime StoredUtc { get; set; }

    /// <summary>Gets or sets the statement's timestamp, in UTC.</summary>
    public DateTime? TimestampUtc { get; set; }

    /// <summary>Gets or sets the verb id.</summary>
    public string Verb { get; set; }

    /// <summary>Gets or sets the type of the object: Activity, Agent, Group, StatementRef or SubStatement.</summary>
    public string ObjectType { get; set; }

    /// <summary>Gets or sets the id of the object: the activity id, the agent key or the referenced statement id.</summary>
    public string ObjectId { get; set; }

    /// <summary>Gets or sets the key of the actor (see the agent keys of the LRS core), or <see langword="null"/> for an anonymous group.</summary>
    public string ActorKey { get; set; }

    /// <summary>Gets or sets the account name of the actor when it is identified by an account.</summary>
    public string ActorAccountName { get; set; }

    /// <summary>Gets or sets the home page of the actor's account when it is identified by an account.</summary>
    public string ActorAccountHomePage { get; set; }

    /// <summary>Gets or sets the registration of the statement's context, a UUID in lower case.</summary>
    public string Registration { get; set; }

    /// <summary>Gets or sets the key of the credential that stored the statement, which "read own" access compares.</summary>
    public string AuthorityKey { get; set; }

    /// <summary>Gets or sets the OpenID client id that stored the statement, when a client stored it.</summary>
    public string ClientId { get; set; }

    /// <summary>Gets or sets whether the statement voids another one.</summary>
    public bool IsVoiding { get; set; }

    /// <summary>Gets or sets the statement the object refers to, when the object is a statement reference.</summary>
    public string TargetStatementId { get; set; }

    /// <summary>Gets or sets whether a voiding statement voided this statement.</summary>
    public bool Voided { get; set; }

    /// <summary>Gets or sets the voiding statement.</summary>
    public string VoidedBy { get; set; }

    /// <summary>Gets or sets the attachments whose content the LRS keeps.</summary>
    public IList<XapiAttachmentFile> Attachments { get; set; } = [];

    /// <summary>Gets or sets the keys of the statement itself.</summary>
    public XapiStatementKeys OwnKeys { get; set; } = new();

    /// <summary>Gets or sets the keys the queries filter on: the statement's own and those of the statement it refers
    /// to, through any number of statement references.</summary>
    public XapiStatementKeys Keys { get; set; } = new();
}

/// <summary>
/// The content of an attachment, kept in the protected file area of attachments.
/// </summary>
public sealed class XapiAttachmentFile
{
    /// <summary>Gets or sets the SHA-2 hash of the content, in lower case hex.</summary>
    public string Sha2 { get; set; }

    /// <summary>Gets or sets the file in the protected file area.</summary>
    public string FileId { get; set; }

    /// <summary>Gets or sets the content type.</summary>
    public string ContentType { get; set; }

    /// <summary>Gets or sets the length in bytes.</summary>
    public long Length { get; set; }
}

/// <summary>
/// The values statement queries filter on.
/// </summary>
public sealed class XapiStatementKeys
{
    /// <summary>Gets or sets the agent keys: direct for the actor and an agent object, related for the others.</summary>
    public IList<XapiStatementKey> Agents { get; set; } = [];

    /// <summary>Gets or sets the activity ids: direct for the object, related for the context and a sub-statement.</summary>
    public IList<XapiStatementKey> Activities { get; set; } = [];

    /// <summary>Gets or sets the verb ids.</summary>
    public IList<string> Verbs { get; set; } = [];

    /// <summary>Gets or sets the registrations.</summary>
    public IList<string> Registrations { get; set; } = [];
}

/// <summary>
/// A value a statement query filters on, and whether it matches without the <c>related_agents</c> or
/// <c>related_activities</c> option.
/// </summary>
public sealed class XapiStatementKey
{
    /// <summary>Gets or sets the value.</summary>
    public string Value { get; set; }

    /// <summary>Gets or sets whether the value matches without the related option.</summary>
    public bool Direct { get; set; }
}
