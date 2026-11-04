using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Consent;

/// <summary>
/// Whether a person may be contacted: on a channel, at an address, and optionally as a record of a kind.
/// </summary>
public sealed class ConsentQuery
{
    /// <summary>
    /// Gets the channel, one of <see cref="ConsentChannels"/>.
    /// </summary>
    public string Channel { get; init; }

    /// <summary>
    /// Gets the address on the channel, such as a phone number in E.164 form or an e-mail address; or
    /// <see langword="null"/>.
    /// </summary>
    public string Address { get; init; }

    /// <summary>
    /// Gets the kind of the record <see cref="ContactId"/> names, one of <see cref="ConsentContactKinds"/>; a CRM
    /// contact when it's empty.
    /// </summary>
    public string ContactKind { get; init; }

    /// <summary>
    /// Gets the id of the record the person is, or <see langword="null"/>.
    /// </summary>
    public string ContactId { get; init; }

    /// <summary>
    /// Gets the CRM contact the caller already loaded, so a source doesn't load it again; or <see langword="null"/>.
    /// </summary>
    public ContentItem Contact { get; init; }

    /// <summary>
    /// Gets the sources not to ask, by name, such as the do-not-call lists for a caller that screens them itself.
    /// </summary>
    public IReadOnlyCollection<string> ExcludedSources { get; init; } = [];
}
