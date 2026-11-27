using System.Security.Claims;
using System.Text.RegularExpressions;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.SiteResources;
using CrestApps.OrchardCore.SiteResources.Models;
using CrestApps.OrchardCore.SiteResources.Services;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Documents;
using OrchardCore.Environment.Shell.Descriptor.Models;
using OrchardCore.Liquid;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Templates.Models;

namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// The signed-in user's theme draft over Orchard Core's front-end and admin templates and the site resources.
/// </summary>
internal sealed partial class ThemeDesignDraftService : IThemeDesignDraftService
{
    /// <summary>
    /// The longest template name: the shape type or alternate it renders.
    /// </summary>
    public const int MaxTemplateNameLength = 255;

    /// <summary>
    /// The largest template a draft may hold, in characters.
    /// </summary>
    public const int MaxTemplateLength = 512 * 1024;

    /// <summary>
    /// The most changes one draft may hold.
    /// </summary>
    public const int MaxChanges = 200;

    private static readonly string[] _layoutResourceTypes = ["Meta", "HeadLink", "Stylesheet", "HeadScript", "FootScript"];

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ThemeDesignDraftStore _draftStore;
    private readonly IDocumentManager<TemplatesDocument> _templatesManager;
    private readonly IDocumentManager<AdminTemplatesDocument> _adminTemplatesManager;
    private readonly IDocumentManager<DictionaryDocument<SiteResource>> _resourcesDocumentManager;
    private readonly INamedCatalogManager<SiteResource> _resourceManager;
    private readonly SiteResourceValidator _resourceValidator;
    private readonly ISiteResourceService _siteResourceService;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly ShellDescriptor _shellDescriptor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public ThemeDesignDraftService(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        ThemeDesignDraftStore draftStore,
        IDocumentManager<TemplatesDocument> templatesManager,
        IDocumentManager<AdminTemplatesDocument> adminTemplatesManager,
        IDocumentManager<DictionaryDocument<SiteResource>> resourcesDocumentManager,
        INamedCatalogManager<SiteResource> resourceManager,
        SiteResourceValidator resourceValidator,
        ISiteResourceService siteResourceService,
        ILiquidTemplateManager liquidTemplateManager,
        ShellDescriptor shellDescriptor,
        IClock clock,
        IStringLocalizer<ThemeDesignDraftService> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _draftStore = draftStore;
        _templatesManager = templatesManager;
        _adminTemplatesManager = adminTemplatesManager;
        _resourcesDocumentManager = resourcesDocumentManager;
        _resourceManager = resourceManager;
        _resourceValidator = resourceValidator;
        _siteResourceService = siteResourceService;
        _liquidTemplateManager = liquidTemplateManager;
        _shellDescriptor = shellDescriptor;
        _clock = clock;
        S = stringLocalizer;
    }

    private ClaimsPrincipal User
        => _httpContextAccessor.HttpContext?.User;

    private string UserId
        => User?.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <inheritdoc />
    public Task<ThemeDesignDraft> GetDraftAsync()
        => _draftStore.GetAsync(UserId);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ThemeDesignChange>> GetChangesAsync()
    {
        var draft = await GetDraftAsync();

        if (draft is null)
        {
            return [];
        }

        var siteTemplates = await _templatesManager.GetOrCreateImmutableAsync();
        var adminTemplates = await _adminTemplatesManager.GetOrCreateImmutableAsync();
        var resources = await _resourcesDocumentManager.GetOrCreateImmutableAsync();

        var changes = draft.Templates.Select(template => new ThemeDesignChange
        {
            Kind = template.Scope == ThemeDesignScope.Admin ? ThemeDesignChangeKind.AdminTemplate : ThemeDesignChangeKind.SiteTemplate,
            Name = template.Name,
            ModifiedUtc = template.ModifiedUtc,
            Status = GetStatus(template.Removed, (template.Scope == ThemeDesignScope.Admin ? adminTemplates : siteTemplates).Templates.ContainsKey(template.Name)),
        }).Concat(draft.Resources.Select(resource => new ThemeDesignChange
        {
            Kind = ThemeDesignChangeKind.SiteResource,
            Name = resource.Name,
            ModifiedUtc = resource.ModifiedUtc,
            Status = GetStatus(resource.Removed, FindPublishedResource(resources, resource.Name) is not null),
        }));

        return changes
            .OrderBy(change => change.Kind)
            .ThenBy(change => change.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ShapeTemplateSummary>> ListTemplatesAsync(ThemeDesignScope scope)
    {
        if (!await CanManageTemplatesAsync(scope))
        {
            return [];
        }

        var published = await GetPublishedTemplatesAsync(scope);
        var draft = await GetDraftAsync();
        var summaries = new Dictionary<string, ShapeTemplateSummary>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, template) in published.Templates)
        {
            summaries[name] = new ShapeTemplateSummary
            {
                Scope = scope,
                Name = name,
                Description = template.Description,
                Published = true,
                Length = template.Content?.Length ?? 0,
            };
        }

        foreach (var change in draft?.Templates.Where(template => template.Scope == scope) ?? [])
        {
            var exists = summaries.TryGetValue(change.Name, out var summary);

            summary ??= new ShapeTemplateSummary { Scope = scope, Name = change.Name };
            summary.DraftStatus = GetStatus(change.Removed, exists);

            if (!change.Removed)
            {
                summary.Description = change.Description ?? summary.Description;
                summary.Length = change.Content?.Length ?? 0;
            }

            summaries[change.Name] = summary;
        }

        return summaries.Values
            .OrderBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<ShapeTemplateDetails> GetTemplateAsync(ThemeDesignScope scope, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !await CanManageTemplatesAsync(scope))
        {
            return null;
        }

        var published = await GetPublishedTemplatesAsync(scope);
        var change = (await GetDraftAsync())?.FindTemplate(scope, name);
        published.Templates.TryGetValue(name, out var template);

        if (template is null && change is null)
        {
            return null;
        }

        return new ShapeTemplateDetails
        {
            Scope = scope,
            Name = change?.Name ?? name,
            PublishedContent = template?.Content,
            PublishedDescription = template?.Description,
            DraftContent = change is { Removed: false } ? change.Content : null,
            DraftDescription = change is { Removed: false } ? change.Description : null,
            DraftStatus = change is null ? null : GetStatus(change.Removed, template is not null),
        };
    }

    /// <inheritdoc />
    public async Task<ThemeDesignResult> SaveTemplateAsync(ThemeDesignScope scope, string name, string content, string description)
    {
        name = name?.Trim();

        if (await CheckTemplatesAsync(scope) is { } denied)
        {
            return denied;
        }

        var errors = new List<string>();

        if (!IsValidTemplateName(name))
        {
            errors.Add(S["Give the template the name of the shape it renders, such as Layout, Content__BlogPost or Content_Summary__Article: letters, digits, underscores, dashes and dots."]);
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add(S["Enter the template's Liquid."]);
        }
        else if (content.Length > MaxTemplateLength)
        {
            errors.Add(S["A template can hold up to {0:N0} characters.", MaxTemplateLength]);
        }
        else if (!_liquidTemplateManager.Validate(content, out var liquidErrors))
        {
            errors.Add(S["The Liquid doesn't parse: {0}", string.Join(" ", liquidErrors)]);
        }
        else if (IsLayout(name) && GetMissingLayoutParts(content) is { Count: > 0 } missing)
        {
            errors.Add(S["A layout must keep {0}, or pages lose their content, styles or scripts.", string.Join(", ", missing)]);
        }

        if (errors.Count > 0)
        {
            return ThemeDesignResult.Failed(errors);
        }

        var draft = await LoadOrCreateDraftAsync();
        var change = draft.FindTemplate(scope, name);

        if (change is null)
        {
            if (draft.ChangeCount >= MaxChanges)
            {
                return ThemeDesignResult.Failed(S["A draft can hold up to {0} changes. Publish or discard it first.", MaxChanges]);
            }

            change = new TemplateDraft { Scope = scope, Name = name };
            draft.Templates.Add(change);
        }

        change.Content = content;
        change.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        change.Removed = false;
        change.ModifiedUtc = _clock.UtcNow;

        await SaveDraftAsync(draft);

        return ThemeDesignResult.Success();
    }

    /// <inheritdoc />
    public async Task<ThemeDesignResult> RemoveTemplateAsync(ThemeDesignScope scope, string name)
    {
        if (await CheckTemplatesAsync(scope) is { } denied)
        {
            return denied;
        }

        var published = await GetPublishedTemplatesAsync(scope);
        var draft = await LoadOrCreateDraftAsync();
        var change = draft.FindTemplate(scope, name);

        if (!published.Templates.ContainsKey(name ?? string.Empty))
        {
            if (change is null)
            {
                return ThemeDesignResult.Failed(S["There is no template named '{0}' to remove.", name]);
            }

            // The draft added it: removing it just drops it from the draft.
            draft.Templates.Remove(change);
        }
        else
        {
            if (change is null)
            {
                if (draft.ChangeCount >= MaxChanges)
                {
                    return ThemeDesignResult.Failed(S["A draft can hold up to {0} changes. Publish or discard it first.", MaxChanges]);
                }

                change = new TemplateDraft { Scope = scope, Name = name };
                draft.Templates.Add(change);
            }

            change.Removed = true;
            change.Content = null;
            change.Description = null;
            change.ModifiedUtc = _clock.UtcNow;
        }

        await SaveDraftAsync(draft);

        return ThemeDesignResult.Success();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SiteResourceSummary>> ListResourcesAsync()
    {
        if (!await CanAsync(SiteResourcesPermissions.ManageSiteResources))
        {
            return [];
        }

        var published = await _resourcesDocumentManager.GetOrCreateImmutableAsync();
        var draft = await GetDraftAsync();
        var summaries = new Dictionary<string, SiteResourceSummary>(StringComparer.OrdinalIgnoreCase);

        foreach (var resource in published.Records.Values.Where(resource => !string.IsNullOrEmpty(resource.Name)))
        {
            summaries[resource.Name] = ToSummary(resource, published: true);
        }

        foreach (var change in draft?.Resources ?? [])
        {
            var exists = summaries.TryGetValue(change.Name, out var summary);

            if (!change.Removed && change.Resource is not null)
            {
                summary = ToSummary(change.Resource, exists);
            }

            summary ??= new SiteResourceSummary { Name = change.Name };
            summary.DraftStatus = GetStatus(change.Removed, exists);
            summaries[change.Name] = summary;
        }

        return summaries.Values
            .OrderBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<SiteResourceDetails> GetResourceAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !await CanAsync(SiteResourcesPermissions.ManageSiteResources))
        {
            return null;
        }

        var published = FindPublishedResource(await _resourcesDocumentManager.GetOrCreateImmutableAsync(), name);
        var change = (await GetDraftAsync())?.FindResource(name);

        if (published is null && change is null)
        {
            return null;
        }

        return new SiteResourceDetails
        {
            Published = published?.Clone(),
            Draft = change is { Removed: false } ? change.Resource?.Clone() : null,
            DraftStatus = change is null ? null : GetStatus(change.Removed, published is not null),
        };
    }

    /// <inheritdoc />
    public async Task<ThemeDesignResult> SaveResourceAsync(SiteResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (!await CanAsync(SiteResourcesPermissions.ManageSiteResources))
        {
            return ThemeDesignResult.Failed(S["You aren't allowed to manage the site's stylesheets and scripts."]);
        }

        var candidate = resource.Clone();
        candidate.Name = candidate.Name?.Trim();
        candidate.Themes = (candidate.Themes ?? [])
            .Where(theme => !string.IsNullOrWhiteSpace(theme))
            .Select(theme => theme.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        candidate.Dependencies = (candidate.Dependencies ?? [])
            .Where(dependency => !string.IsNullOrWhiteSpace(dependency))
            .Select(dependency => dependency.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var published = await _resourcesDocumentManager.GetOrCreateImmutableAsync();
        var existing = FindPublishedResource(published, candidate.Name);

        // The draft keeps the published resource's id, so publishing updates it instead of adding another.
        candidate.ItemId = existing?.ItemId ?? candidate.ItemId ?? UniqueId.GenerateId();

        var draft = await LoadOrCreateDraftAsync();

        // Names are unique across the published resources and the resources the draft adds.
        var others = published.Records.Values
            .Where(other => draft.FindResource(other.Name) is not { Removed: true })
            .Select(other => (other.ItemId, other.Name))
            .Concat(draft.Resources
                .Where(change => !change.Removed && change.Resource is not null)
                .Select(change => (change.Resource.ItemId, change.Resource.Name)));

        var errors = _resourceValidator.Validate(candidate, others)
            .Select(error => error.ErrorMessage)
            .ToList();

        if (errors.Count > 0)
        {
            return ThemeDesignResult.Failed(errors);
        }

        var minifyErrors = _siteResourceService.Prepare(candidate);

        if (minifyErrors.Count > 0)
        {
            return ThemeDesignResult.Failed([S["The {0} has errors and can't be minified. Fix them, or turn minification off for it.", candidate.Type == SiteResourceType.Script ? S["script"] : S["stylesheet"]], .. minifyErrors]);
        }

        var change = draft.FindResource(candidate.Name);

        if (change is null)
        {
            if (draft.ChangeCount >= MaxChanges)
            {
                return ThemeDesignResult.Failed(S["A draft can hold up to {0} changes. Publish or discard it first.", MaxChanges]);
            }

            change = new SiteResourceDraft();
            draft.Resources.Add(change);
        }

        change.Name = candidate.Name;
        change.Resource = candidate;
        change.Removed = false;
        change.ModifiedUtc = _clock.UtcNow;

        await SaveDraftAsync(draft);

        return ThemeDesignResult.Success();
    }

    /// <inheritdoc />
    public async Task<ThemeDesignResult> RemoveResourceAsync(string name)
    {
        if (!await CanAsync(SiteResourcesPermissions.ManageSiteResources))
        {
            return ThemeDesignResult.Failed(S["You aren't allowed to manage the site's stylesheets and scripts."]);
        }

        var published = FindPublishedResource(await _resourcesDocumentManager.GetOrCreateImmutableAsync(), name);
        var draft = await LoadOrCreateDraftAsync();
        var change = draft.FindResource(name);

        if (published is null)
        {
            if (change is null)
            {
                return ThemeDesignResult.Failed(S["There is no site resource named '{0}' to remove.", name]);
            }

            draft.Resources.Remove(change);
        }
        else
        {
            if (change is null)
            {
                if (draft.ChangeCount >= MaxChanges)
                {
                    return ThemeDesignResult.Failed(S["A draft can hold up to {0} changes. Publish or discard it first.", MaxChanges]);
                }

                change = new SiteResourceDraft();
                draft.Resources.Add(change);
            }

            change.Name = published.Name;
            change.Resource = null;
            change.Removed = true;
            change.ModifiedUtc = _clock.UtcNow;
        }

        await SaveDraftAsync(draft);

        return ThemeDesignResult.Success();
    }

    /// <inheritdoc />
    public async Task<ThemeDesignResult> RevertAsync(ThemeDesignChangeKind kind, string name)
    {
        var draft = await _draftStore.LoadAsync(UserId);

        var removed = kind switch
        {
            ThemeDesignChangeKind.SiteResource => draft?.Resources.RemoveAll(change => string.Equals(change.Name, name, StringComparison.OrdinalIgnoreCase)) > 0,
            ThemeDesignChangeKind.AdminTemplate => draft?.Templates.RemoveAll(change => change.Scope == ThemeDesignScope.Admin && string.Equals(change.Name, name, StringComparison.OrdinalIgnoreCase)) > 0,
            _ => draft?.Templates.RemoveAll(change => change.Scope == ThemeDesignScope.Site && string.Equals(change.Name, name, StringComparison.OrdinalIgnoreCase)) > 0,
        };

        if (!removed)
        {
            return ThemeDesignResult.Failed(S["Your draft has no change to '{0}'.", name]);
        }

        draft.ModifiedUtc = _clock.UtcNow;
        await _draftStore.SaveAsync(draft);

        return ThemeDesignResult.Success();
    }

    /// <inheritdoc />
    public async Task<ThemeDesignResult> PublishAsync()
    {
        var draft = await _draftStore.LoadAsync(UserId);

        if (draft is null || draft.ChangeCount == 0)
        {
            return ThemeDesignResult.Failed(S["Your draft has no change to publish."]);
        }

        // Everything is checked before anything is applied, so a draft is published whole or not at all.
        var errors = new List<string>();

        if (draft.Templates.Any(change => change.Scope == ThemeDesignScope.Site) && !await CanAsync(ThemeDesignerPermissions.ManageTemplates))
        {
            errors.Add(S["You aren't allowed to manage the front-end templates."]);
        }

        if (draft.Templates.Any(change => change.Scope == ThemeDesignScope.Admin) && !await CanAsync(ThemeDesignerPermissions.ManageAdminTemplates))
        {
            errors.Add(S["You aren't allowed to manage the admin templates."]);
        }

        if (draft.Resources.Count > 0 && !await CanAsync(SiteResourcesPermissions.ManageSiteResources))
        {
            errors.Add(S["You aren't allowed to manage the site's stylesheets and scripts."]);
        }

        if (errors.Count > 0)
        {
            return ThemeDesignResult.Failed(errors);
        }

        var resourceWork = new List<(SiteResourceDraft Change, SiteResource Target, bool IsNew)>();

        foreach (var change in draft.Resources)
        {
            var existing = await _resourceManager.FindByNameAsync(change.Name);

            if (change.Removed)
            {
                if (existing is not null)
                {
                    resourceWork.Add((change, existing, false));
                }

                continue;
            }

            var target = existing ?? await _resourceManager.NewAsync();

            CopyResource(change.Resource, target);

            var validation = await _resourceManager.ValidateAsync(target);

            if (!validation.Succeeded)
            {
                errors.AddRange(validation.Errors.Select(error => $"{change.Name}: {error.ErrorMessage}"));
            }

            resourceWork.Add((change, target, existing is null));
        }

        if (errors.Count > 0)
        {
            return ThemeDesignResult.Failed(errors);
        }

        await ApplyTemplatesAsync(_templatesManager, draft.Templates.Where(change => change.Scope == ThemeDesignScope.Site).ToList());
        await ApplyTemplatesAsync(_adminTemplatesManager, draft.Templates.Where(change => change.Scope == ThemeDesignScope.Admin).ToList());

        foreach (var (change, target, isNew) in resourceWork)
        {
            if (change.Removed)
            {
                await _resourceManager.DeleteAsync(target);
            }
            else if (isNew)
            {
                await _resourceManager.CreateAsync(target);
            }
            else
            {
                await _resourceManager.UpdateAsync(target);
            }
        }

        await _draftStore.DeleteAsync(draft.UserId);

        return ThemeDesignResult.Success();
    }

    /// <inheritdoc />
    public async Task<ThemeDesignResult> DiscardAsync()
    {
        var userId = UserId;

        if (string.IsNullOrEmpty(userId) || await _draftStore.GetAsync(userId) is null)
        {
            return ThemeDesignResult.Failed(S["You have no draft to discard."]);
        }

        await _draftStore.DeleteAsync(userId);

        return ThemeDesignResult.Success();
    }

    /// <inheritdoc />
    public async Task<ThemeDesignResult> SetPreviewAsync(bool preview)
    {
        var draft = await _draftStore.LoadAsync(UserId);

        if (draft is null)
        {
            return ThemeDesignResult.Failed(S["You have no draft to preview."]);
        }

        draft.Preview = preview;
        await _draftStore.SaveAsync(draft);

        return ThemeDesignResult.Success();
    }

    /// <summary>
    /// Gets whether <paramref name="name"/> can name a shape template.
    /// </summary>
    /// <param name="name">The name to check.</param>
    internal static bool IsValidTemplateName(string name)
        => !string.IsNullOrEmpty(name) && name.Length <= MaxTemplateNameLength && TemplateNamePattern().IsMatch(name);

    /// <summary>
    /// Gets whether a template renders the page layout: <c>Layout</c> or one of its alternates.
    /// </summary>
    /// <param name="name">The template name.</param>
    internal static bool IsLayout(string name)
        => string.Equals(name, "Layout", StringComparison.OrdinalIgnoreCase) ||
            (name?.StartsWith("Layout_", StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>
    /// Gets the tags a Liquid layout leaves out that every page needs: the page's content and the places the resource
    /// manager writes its meta tags, links, stylesheets and scripts.
    /// </summary>
    /// <param name="content">The layout's Liquid.</param>
    internal static IReadOnlyList<string> GetMissingLayoutParts(string content)
    {
        var missing = new List<string>();

        if (!RenderBodyPattern().IsMatch(content))
        {
            missing.Add("{% render_body %}");
        }

        foreach (var type in _layoutResourceTypes)
        {
            if (!Regex.IsMatch(content, $@"\{{%-?\s*resources\s+type:\s*[""']{type}[""']", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                missing.Add($"{{% resources type: \"{type}\" %}}");
            }
        }

        return missing;
    }

    private static ThemeDesignChangeStatus GetStatus(bool removed, bool published)
        => removed ? ThemeDesignChangeStatus.Removed : published ? ThemeDesignChangeStatus.Changed : ThemeDesignChangeStatus.Added;

    private static SiteResource FindPublishedResource(DictionaryDocument<SiteResource> document, string name)
        => string.IsNullOrEmpty(name)
            ? null
            : document.Records.Values.FirstOrDefault(resource => string.Equals(resource.Name, name, StringComparison.OrdinalIgnoreCase));

    private static SiteResourceSummary ToSummary(SiteResource resource, bool published)
        => new()
        {
            Name = resource.Name,
            Description = resource.Description,
            Type = resource.Type,
            Target = resource.Target,
            Placement = resource.Placement,
            Enabled = resource.Enabled,
            Published = published,
            Length = resource.Content?.Length ?? 0,
        };

    // Copies what the designer controls; the id, the stamps and what is served stay the target's own, and the
    // catalog's handlers work out what is served again.
    private static void CopyResource(SiteResource source, SiteResource target)
    {
        target.Name = source.Name;
        target.Description = source.Description;
        target.Type = source.Type;
        target.Content = source.Content;
        target.Minify = source.Minify;
        target.Target = source.Target;
        target.Themes = [.. source.Themes ?? []];
        target.Placement = source.Placement;
        target.Location = source.Location;
        target.Dependencies = [.. source.Dependencies ?? []];
        target.Position = source.Position;
        target.Enabled = source.Enabled;
    }

    private static async Task ApplyTemplatesAsync<TDocument>(IDocumentManager<TDocument> documentManager, List<TemplateDraft> changes)
        where TDocument : TemplatesDocument, new()
    {
        if (changes.Count == 0)
        {
            return;
        }

        var document = await documentManager.GetOrCreateMutableAsync();

        foreach (var change in changes)
        {
            if (change.Removed)
            {
                document.Templates.Remove(change.Name);
            }
            else
            {
                document.Templates[change.Name] = new Template
                {
                    Content = change.Content,
                    Description = change.Description,
                };
            }
        }

        await documentManager.UpdateAsync(document);
    }

    private async Task<TemplatesDocument> GetPublishedTemplatesAsync(ThemeDesignScope scope)
        => scope == ThemeDesignScope.Admin
            ? await _adminTemplatesManager.GetOrCreateImmutableAsync()
            : await _templatesManager.GetOrCreateImmutableAsync();

    private async Task<ThemeDesignDraft> LoadOrCreateDraftAsync()
    {
        var userId = UserId;
        var draft = await _draftStore.LoadAsync(userId);

        if (draft is not null)
        {
            return draft;
        }

        var now = _clock.UtcNow;

        return new ThemeDesignDraft
        {
            UserId = userId,
            UserName = User?.Identity?.Name,
            CreatedUtc = now,
            ModifiedUtc = now,
        };
    }

    private Task SaveDraftAsync(ThemeDesignDraft draft)
    {
        draft.ModifiedUtc = _clock.UtcNow;

        return _draftStore.SaveAsync(draft);
    }

    private async Task<ThemeDesignResult> CheckTemplatesAsync(ThemeDesignScope scope)
    {
        if (string.IsNullOrEmpty(UserId))
        {
            return ThemeDesignResult.Failed(S["Sign in to design the theme."]);
        }

        if (scope == ThemeDesignScope.Admin && !IsAdminTemplatesEnabled())
        {
            return ThemeDesignResult.Failed(S["Admin templates are off. Enable the Admin Templates feature first."]);
        }

        if (!await CanManageTemplatesAsync(scope))
        {
            return ThemeDesignResult.Failed(scope == ThemeDesignScope.Admin
                ? S["You aren't allowed to manage the admin templates."]
                : S["You aren't allowed to manage the front-end templates."]);
        }

        return null;
    }

    private bool IsAdminTemplatesEnabled()
        => _shellDescriptor.Features.Any(feature => feature.Id == OrchardCoreFeatureIds.AdminTemplates);

    private Task<bool> CanManageTemplatesAsync(ThemeDesignScope scope)
        => CanAsync(scope == ThemeDesignScope.Admin ? ThemeDesignerPermissions.ManageAdminTemplates : ThemeDesignerPermissions.ManageTemplates);

    private async Task<bool> CanAsync(Permission permission)
        => User is { } user && !string.IsNullOrEmpty(UserId) && await _authorizationService.AuthorizeAsync(user, permission);

    [GeneratedRegex("^[A-Za-z0-9_][A-Za-z0-9_.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex TemplateNamePattern();

    [GeneratedRegex(@"\{%-?\s*render_body\s*-?%\}", RegexOptions.CultureInvariant)]
    private static partial Regex RenderBodyPattern();
}
