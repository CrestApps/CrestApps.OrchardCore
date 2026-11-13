using CrestApps.OrchardCore.SiteResources.Models;
using CrestApps.OrchardCore.ThemeDesigner.Models;

namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// Changes the signed-in user's theme draft: front-end and admin templates and site resources that only they see
/// until they publish it. Every method checks the permission the change needs.
/// </summary>
public interface IThemeDesignDraftService
{
    /// <summary>
    /// Gets the signed-in user's draft, or <see langword="null"/> when they have none.
    /// </summary>
    Task<ThemeDesignDraft> GetDraftAsync();

    /// <summary>
    /// Gets the changes in the signed-in user's draft.
    /// </summary>
    Task<IReadOnlyList<ThemeDesignChange>> GetChangesAsync();

    /// <summary>
    /// Lists the published templates of a side of the site, merged with the draft's changes.
    /// </summary>
    /// <param name="scope">The side of the site.</param>
    Task<IReadOnlyList<ShapeTemplateSummary>> ListTemplatesAsync(ThemeDesignScope scope);

    /// <summary>
    /// Gets a template as it is published and as the draft has it, or <see langword="null"/> when neither has it.
    /// </summary>
    /// <param name="scope">The side of the site.</param>
    /// <param name="name">The template name.</param>
    Task<ShapeTemplateDetails> GetTemplateAsync(ThemeDesignScope scope, string name);

    /// <summary>
    /// Adds a template to the draft or changes it there, after checking that the Liquid parses.
    /// </summary>
    /// <param name="scope">The side of the site.</param>
    /// <param name="name">The template name: the shape type or alternate it renders.</param>
    /// <param name="content">The Liquid template.</param>
    /// <param name="description">What the template is for.</param>
    Task<ThemeDesignResult> SaveTemplateAsync(ThemeDesignScope scope, string name, string content, string description);

    /// <summary>
    /// Marks a published template for removal in the draft, or drops a template the draft added.
    /// </summary>
    /// <param name="scope">The side of the site.</param>
    /// <param name="name">The template name.</param>
    Task<ThemeDesignResult> RemoveTemplateAsync(ThemeDesignScope scope, string name);

    /// <summary>
    /// Lists the published site resources, merged with the draft's changes.
    /// </summary>
    Task<IReadOnlyList<SiteResourceSummary>> ListResourcesAsync();

    /// <summary>
    /// Gets a site resource as it is published and as the draft has it, or <see langword="null"/> when neither has it.
    /// </summary>
    /// <param name="name">The resource name.</param>
    Task<SiteResourceDetails> GetResourceAsync(string name);

    /// <summary>
    /// Adds a site resource to the draft or changes it there, matched by name, after validating and minifying it.
    /// </summary>
    /// <param name="resource">The resource as the draft should have it.</param>
    Task<ThemeDesignResult> SaveResourceAsync(SiteResource resource);

    /// <summary>
    /// Marks a published site resource for removal in the draft, or drops a resource the draft added.
    /// </summary>
    /// <param name="name">The resource name.</param>
    Task<ThemeDesignResult> RemoveResourceAsync(string name);

    /// <summary>
    /// Drops one change from the draft, so the published item is left as it is.
    /// </summary>
    /// <param name="kind">What the change is about.</param>
    /// <param name="name">The template or resource name.</param>
    Task<ThemeDesignResult> RevertAsync(ThemeDesignChangeKind kind, string name);

    /// <summary>
    /// Applies every change in the draft to the published templates and site resources, then deletes the draft.
    /// Nothing is applied when any change can't be.
    /// </summary>
    Task<ThemeDesignResult> PublishAsync();

    /// <summary>
    /// Deletes the draft without publishing it.
    /// </summary>
    Task<ThemeDesignResult> DiscardAsync();

    /// <summary>
    /// Turns on or off the preview of the draft while the designer browses the site.
    /// </summary>
    /// <param name="preview">Whether to show the draft.</param>
    Task<ThemeDesignResult> SetPreviewAsync(bool preview);
}
