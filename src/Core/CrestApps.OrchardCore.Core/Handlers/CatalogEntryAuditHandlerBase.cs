using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Core.Handlers;

/// <summary>
/// The base of a catalog entry handler that stamps when an entry was created and changed, and by whom.
/// </summary>
/// <remarks>
/// <see cref="AuditedCatalogEntryHandlerBase{T}"/> builds on it for entries that implement
/// <see cref="CrestApps.OrchardCore.Models.IAuditedModel"/>. Entries declared in CrestApps.Core carry the same members
/// without that interface, so their handlers derive from this class, say how the created members are set in
/// <see cref="SetCreated"/>, and call <see cref="StampCreated"/> and <see cref="StampModified"/> from the life-cycle
/// events they already handle.
/// </remarks>
/// <typeparam name="T">The catalog entry type.</typeparam>
public abstract class CatalogEntryAuditHandlerBase<T> : CatalogEntryHandlerBase<T>
    where T : CatalogItem, IModifiedUtcAwareModel
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogEntryAuditHandlerBase{T}"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The accessor that gives the user making the change.</param>
    /// <param name="clock">The clock the created and modified dates are read from.</param>
    protected CatalogEntryAuditHandlerBase(
        IHttpContextAccessor httpContextAccessor,
        IClock clock)
    {
        _httpContextAccessor = httpContextAccessor;
        Clock = clock;
    }

    /// <summary>
    /// Gets the clock the created and modified dates are read from.
    /// </summary>
    protected IClock Clock { get; }

    /// <summary>
    /// Stamps a new entry with the current date and, when a user makes the change, with that user as its owner and
    /// author.
    /// </summary>
    /// <param name="entry">The new entry.</param>
    protected void StampCreated(T entry)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        SetCreated(
            entry,
            Clock.UtcNow,
            user?.FindFirstValue(ClaimTypes.NameIdentifier),
            user?.Identity?.Name,
            user is not null);
    }

    /// <summary>
    /// Stamps a changed entry with the current date.
    /// </summary>
    /// <param name="entry">The changed entry.</param>
    protected void StampModified(T entry)
        => entry.ModifiedUtc = Clock.UtcNow;

    /// <summary>
    /// Sets the created members of a new entry.
    /// </summary>
    /// <param name="entry">The new entry.</param>
    /// <param name="createdUtc">The date the entry was created.</param>
    /// <param name="ownerId">The identifier of the user making the change, if any.</param>
    /// <param name="author">The name of the user making the change, if any.</param>
    /// <param name="hasUser">Whether a user makes the change; the owner and author are left unchanged when not.</param>
    protected abstract void SetCreated(T entry, DateTime createdUtc, string ownerId, string author, bool hasUser);
}
