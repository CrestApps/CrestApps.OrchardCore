using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Learning.Xapi;

/// <summary>
/// Who made a request to the LRS, and what it may do. A credential comes from an OpenID application (a bearer token
/// of the <c>Api</c> scheme, or HTTP Basic with the application's client id and secret) whose roles hold the LRS
/// permissions, or from a launch token that opens the LRS for one learner, registration and activity. The sign-in
/// cookie never makes a credential.
/// </summary>
public sealed class XapiCredential
{
    /// <summary>Gets the OpenID client id, or <see langword="null"/> for a launch token.</summary>
    public string ClientId { get; init; }

    /// <summary>Gets the name of the client.</summary>
    public string Name { get; init; }

    /// <summary>Gets the agent the LRS writes as the <c>authority</c> of the statements this credential stores.</summary>
    public JsonObject Authority { get; init; }

    /// <summary>Gets the key "read own" access compares with the statements' <see cref="Models.XapiStatementRecord.AuthorityKey"/>.</summary>
    public string AuthorityKey { get; init; }

    /// <summary>Gets whether the credential may store statements.</summary>
    public bool CanWriteStatements { get; init; }

    /// <summary>Gets whether the credential may read every statement.</summary>
    public bool CanReadAllStatements { get; init; }

    /// <summary>Gets whether the credential may read the statements it stored.</summary>
    public bool CanReadOwnStatements { get; init; }

    /// <summary>Gets whether the credential may read and write documents and read the activities and agents resources.</summary>
    public bool CanManageDocuments { get; init; }

    /// <summary>Gets the launch the credential is limited to, or <see langword="null"/> for an OpenID application.</summary>
    public XapiLaunchScope Launch { get; init; }

    /// <summary>Gets whether the credential is a launch token.</summary>
    public bool IsLaunch => Launch is not null;
}

/// <summary>
/// What a launch token opens: the statements of one actor in one registration, and the documents of that actor and
/// activity. A module that launches content (cmi5) makes the token and keeps its own values in <see cref="Properties"/>.
/// </summary>
public sealed class XapiLaunchScope
{
    /// <summary>Gets or sets the kind of launch, such as <c>cmi5</c>.</summary>
    public string Kind { get; set; }

    /// <summary>Gets or sets the session of the launch.</summary>
    public string SessionId { get; set; }

    /// <summary>Gets or sets the registration, a UUID in lower case.</summary>
    public string Registration { get; set; }

    /// <summary>Gets or sets the activity id of the launched content.</summary>
    public string ActivityId { get; set; }

    /// <summary>Gets or sets the key of the actor.</summary>
    public string ActorKey { get; set; }

    /// <summary>Gets or sets when the token expires, in UTC.</summary>
    public DateTime ExpiresUtc { get; set; }

    /// <summary>Gets or sets the state ids the launch may read but not change, such as the launch data a module wrote.</summary>
    public IList<string> ReadOnlyStateIds { get; set; } = [];

    /// <summary>Gets or sets the launching module's own values, such as the enrollment and the course item.</summary>
    public IDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
