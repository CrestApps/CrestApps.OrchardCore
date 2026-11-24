using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Xapi.Models;

/// <summary>
/// A document of the Learning Record Store: a state (an activity, an agent, an optional registration and a state id),
/// an activity profile (an activity and a profile id) or an agent profile (an agent and a profile id), with its
/// content, its content type and the ETag the concurrency rules compare.
/// </summary>
public sealed class XapiDocument : CatalogItem
{
    /// <summary>Gets or sets the kind of document: one of <see cref="XapiDocumentKinds"/>.</summary>
    public string Kind { get; set; }

    /// <summary>Gets or sets the activity id of a state or an activity profile.</summary>
    public string ActivityId { get; set; }

    /// <summary>Gets or sets the agent of a state or an agent profile, as JSON.</summary>
    public string Agent { get; set; }

    /// <summary>Gets or sets the key of the agent.</summary>
    public string AgentKey { get; set; }

    /// <summary>Gets or sets the registration of a state, a UUID in lower case, or <see langword="null"/>.</summary>
    public string Registration { get; set; }

    /// <summary>Gets or sets the state id or the profile id.</summary>
    public string DocumentId { get; set; }

    /// <summary>Gets or sets the content type.</summary>
    public string ContentType { get; set; }

    /// <summary>Gets or sets the content.</summary>
    public byte[] Content { get; set; } = [];

    /// <summary>Gets or sets the ETag, quoted.</summary>
    public string ETag { get; set; }

    /// <summary>Gets or sets when the document last changed, in UTC.</summary>
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>
/// The kinds of documents.
/// </summary>
public static class XapiDocumentKinds
{
    /// <summary>A state.</summary>
    public const string State = "State";

    /// <summary>An activity profile.</summary>
    public const string ActivityProfile = "ActivityProfile";

    /// <summary>An agent profile.</summary>
    public const string AgentProfile = "AgentProfile";
}

/// <summary>
/// The canonical definition the LRS keeps of an activity, joined from the definitions statements carried.
/// </summary>
public sealed class XapiActivityRecord : CatalogItem
{
    /// <summary>Gets or sets the activity id.</summary>
    public string ActivityId { get; set; }

    /// <summary>Gets or sets the definition, as JSON.</summary>
    public string Definition { get; set; }

    /// <summary>Gets or sets when the definition last changed, in UTC.</summary>
    public DateTime UpdatedUtc { get; set; }
}
