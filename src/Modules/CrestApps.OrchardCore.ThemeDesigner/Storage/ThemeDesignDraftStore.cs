using CrestApps.OrchardCore.ThemeDesigner.Models;
using OrchardCore.Documents;
using OrchardCore.Environment.Cache;

namespace CrestApps.OrchardCore.ThemeDesigner.Storage;

/// <summary>
/// Reads and writes the designers' theme drafts, and clears the cached shapes a designer previewed whenever their
/// draft changes.
/// </summary>
public sealed class ThemeDesignDraftStore
{
    private readonly IDocumentManager<ThemeDesignDraftsDocument> _documentManager;
    private readonly ITagCache _tagCache;

    public ThemeDesignDraftStore(
        IDocumentManager<ThemeDesignDraftsDocument> documentManager,
        ITagCache tagCache)
    {
        _documentManager = documentManager;
        _tagCache = tagCache;
    }

    /// <summary>
    /// Gets the cache tag of the shapes a designer rendered while previewing their draft.
    /// </summary>
    /// <param name="userId">The designer's user id.</param>
    public static string GetCacheTag(string userId)
        => "theme-design-draft:" + userId;

    /// <summary>
    /// Gets a designer's draft from the shared cache, to read only, or <see langword="null"/> when they have none.
    /// </summary>
    /// <param name="userId">The designer's user id.</param>
    public async Task<ThemeDesignDraft> GetAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var document = await _documentManager.GetOrCreateImmutableAsync();

        return document.Drafts.GetValueOrDefault(userId);
    }

    /// <summary>
    /// Loads a designer's draft to change it, or <see langword="null"/> when they have none.
    /// </summary>
    /// <param name="userId">The designer's user id.</param>
    public async Task<ThemeDesignDraft> LoadAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var document = await _documentManager.GetOrCreateMutableAsync();

        return document.Drafts.GetValueOrDefault(userId);
    }

    /// <summary>
    /// Saves a designer's draft, or deletes it when it has no change left.
    /// </summary>
    /// <param name="draft">The draft to save.</param>
    public async Task SaveAsync(ThemeDesignDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrEmpty(draft.UserId);

        var document = await _documentManager.GetOrCreateMutableAsync();

        if (draft.ChangeCount == 0)
        {
            document.Drafts.Remove(draft.UserId);
        }
        else
        {
            document.Drafts[draft.UserId] = draft;
        }

        await _documentManager.UpdateAsync(document);
        await _tagCache.RemoveTagAsync(GetCacheTag(draft.UserId));
    }

    /// <summary>
    /// Deletes a designer's draft.
    /// </summary>
    /// <param name="userId">The designer's user id.</param>
    public async Task DeleteAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var document = await _documentManager.GetOrCreateMutableAsync();

        if (document.Drafts.Remove(userId))
        {
            await _documentManager.UpdateAsync(document);
        }

        await _tagCache.RemoveTagAsync(GetCacheTag(userId));
    }
}
