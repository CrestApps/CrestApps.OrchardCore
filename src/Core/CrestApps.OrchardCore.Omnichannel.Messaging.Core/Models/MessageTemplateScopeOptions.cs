using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// The kinds of work, besides the messaging workspace, that use reply templates, such as tickets. A template with no
/// scope is for the messaging workspace; one with a scope is offered only where that kind of work is answered.
/// </summary>
public sealed class MessageTemplateScopeOptions
{
    private readonly Dictionary<string, LocalizationSource> _scopes = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the scopes, by name, with the title the template editor shows.
    /// </summary>
    public IReadOnlyDictionary<string, LocalizationSource> Scopes
        => _scopes;

    /// <summary>
    /// Adds a scope, or retitles it.
    /// </summary>
    /// <param name="name">The scope, such as <c>Tickets</c>.</param>
    /// <param name="title">Its title.</param>
    /// <returns>The options.</returns>
    public MessageTemplateScopeOptions Add(string name, LocalizationSource title)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(title);

        _scopes[name] = title;

        return this;
    }
}
