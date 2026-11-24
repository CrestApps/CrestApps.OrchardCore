using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.Chat.Core.Services;

/// <summary>
/// The admin page the chat widget renders on, and the context entries the <see cref="IAIChatPageContextProvider"/>
/// implementations add for it.
/// </summary>
public sealed class AIChatPageContext
{
    /// <summary>
    /// The most entries the widget sends; the chat sends at most 20 context entries with a message, page title and
    /// selected text included.
    /// </summary>
    public const int MaxEntries = 10;

    /// <summary>
    /// The longest value an entry keeps.
    /// </summary>
    public const int MaxValueLength = 1000;

    private readonly List<ChatClientContextEntry> _entries = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatPageContext"/> class.
    /// </summary>
    /// <param name="httpContext">The request of the page.</param>
    /// <param name="routeValues">The route values of the page.</param>
    public AIChatPageContext(HttpContext httpContext, RouteValueDictionary routeValues)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        HttpContext = httpContext;
        RouteValues = routeValues ?? [];
    }

    /// <summary>
    /// Gets the request of the page.
    /// </summary>
    public HttpContext HttpContext { get; }

    /// <summary>
    /// Gets the route values of the page.
    /// </summary>
    public RouteValueDictionary RouteValues { get; }

    /// <summary>
    /// Gets the entries collected so far.
    /// </summary>
    public IReadOnlyList<ChatClientContextEntry> Entries => _entries;

    /// <summary>
    /// Adds an entry, unless the value is empty or the widget already has <see cref="MaxEntries"/> entries.
    /// </summary>
    /// <param name="description">What the value is, for example "Content item being edited".</param>
    /// <param name="value">The value, cut to <see cref="MaxValueLength"/> characters.</param>
    public void Add(string description, string value)
    {
        if (string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(value) || _entries.Count >= MaxEntries)
        {
            return;
        }

        _entries.Add(new ChatClientContextEntry
        {
            Description = description,
            Value = value.Length > MaxValueLength ? value[..MaxValueLength] : value,
        });
    }

    /// <summary>
    /// Gets a route value as a string, or <see langword="null"/>.
    /// </summary>
    /// <param name="key">The route value name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public string GetRouteValue(string key)
    {
        return RouteValues.TryGetValue(key, out var value) ? value?.ToString() : null;
    }
}
