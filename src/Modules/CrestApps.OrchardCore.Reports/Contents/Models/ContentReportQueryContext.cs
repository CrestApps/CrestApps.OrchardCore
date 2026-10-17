using System.Security.Claims;
using OrchardCore.ContentManagement;
using YesSql;

namespace CrestApps.OrchardCore.Reports.Contents.Models;

/// <summary>
/// Carries what report fields need while one query of a content type data set is read.
/// </summary>
public sealed class ContentReportQueryContext
{
    private readonly Func<string, Task<bool>> _contentTypeAuthorizer;
    private readonly Dictionary<string, bool> _authorizedContentTypes = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentReportQueryContext"/> class.
    /// </summary>
    /// <param name="contentType">The content type the data set reads.</param>
    /// <param name="user">The principal the data is read for.</param>
    /// <param name="contentItems">The content items of the query, in the order they are returned.</param>
    /// <param name="session">The session to load related data from.</param>
    /// <param name="contentTypeAuthorizer">Decides whether <paramref name="user"/> may view the items of a content type.</param>
    public ContentReportQueryContext(
        string contentType,
        ClaimsPrincipal user,
        IReadOnlyList<ContentItem> contentItems,
        ISession session,
        Func<string, Task<bool>> contentTypeAuthorizer)
    {
        ContentType = contentType;
        User = user;
        ContentItems = contentItems ?? [];
        Session = session;
        _contentTypeAuthorizer = contentTypeAuthorizer;
    }

    /// <summary>
    /// Gets the content type the data set reads.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Gets the principal the data is read for.
    /// </summary>
    public ClaimsPrincipal User { get; }

    /// <summary>
    /// Gets the content items of the query, in the order they are returned.
    /// </summary>
    public IReadOnlyList<ContentItem> ContentItems { get; }

    /// <summary>
    /// Gets the session to load related data from.
    /// </summary>
    public ISession Session { get; }

    /// <summary>
    /// Gets a bag where report fields keep what they loaded in <see cref="ContentReportField.PrepareAsync"/>. Key
    /// entries by the report field name so fields do not collide.
    /// </summary>
    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>(StringComparer.Ordinal);

    /// <summary>
    /// Determines whether <see cref="User"/> may view the content items of a content type. Use it before showing
    /// anything of a related content item, such as the display text of a picked item. Results are cached for the
    /// query.
    /// </summary>
    /// <param name="contentType">The content type.</param>
    /// <returns><see langword="true"/> when the user may view the content items of the type.</returns>
    public async Task<bool> CanViewContentTypeAsync(string contentType)
    {
        if (string.IsNullOrEmpty(contentType) || _contentTypeAuthorizer is null)
        {
            return false;
        }

        if (!_authorizedContentTypes.TryGetValue(contentType, out var authorized))
        {
            authorized = await _contentTypeAuthorizer(contentType);
            _authorizedContentTypes[contentType] = authorized;
        }

        return authorized;
    }
}
