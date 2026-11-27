using CrestApps.OrchardCore.ThemeDesigner.Models;
using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.ThemeDesigner.Storage;

/// <summary>
/// The theme drafts of every designer, by user id. The document is cached, so a page checks for its viewer's draft
/// without a database query.
/// </summary>
public sealed class ThemeDesignDraftsDocument : Document
{
    /// <summary>
    /// Gets or sets the drafts, by the id of the user they belong to.
    /// </summary>
    public Dictionary<string, ThemeDesignDraft> Drafts { get; set; } = [];
}
