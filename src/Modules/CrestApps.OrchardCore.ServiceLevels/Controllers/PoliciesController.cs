using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.ServiceLevels.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ServiceLevels.Controllers;

/// <summary>
/// Lists, creates, edits and deletes service level policies. A new policy is created for one scope (the kind of work
/// it is for), passed as <c>?scope=</c>; the scope is fixed after that.
/// </summary>
[Admin]
public sealed class PoliciesController : CatalogAdminController<ServiceLevelPolicy>
{
    private const string IndexRoute = "service-levels";
    private const string IndexRouteName = "ServiceLevelPoliciesIndex";
    private const string CreateRoute = "service-levels/create";
    private const string CreateRouteName = "ServiceLevelPoliciesCreate";

    private readonly IEnumerable<IServiceLevelScopeProvider> _scopes;
    private readonly IEnumerable<IServiceLevelPolicyUsageProvider> _usageProviders;

    /// <summary>
    /// Initializes a new instance of the <see cref="PoliciesController"/> class.
    /// </summary>
    /// <param name="manager">The policy manager.</param>
    /// <param name="scopes">The registered scopes.</param>
    /// <param name="usageProviders">The providers that say which records use a policy.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PoliciesController(
        IServiceLevelPolicyManager manager,
        IEnumerable<IServiceLevelScopeProvider> scopes,
        IEnumerable<IServiceLevelPolicyUsageProvider> usageProviders,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<ServiceLevelPolicy> displayManager,
        INotifier notifier,
        IHtmlLocalizer<PoliciesController> htmlLocalizer,
        IStringLocalizer<PoliciesController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
        _scopes = scopes;
        _usageProviders = usageProviders;
    }

    /// <inheritdoc/>
    protected override Permission ManagePermission
        => ServiceLevelsPermissions.ManageServiceLevelPolicies;

    /// <inheritdoc/>
    protected override LocalizedString CreateDisplayName
        => S["Service level policy"];

    /// <inheritdoc/>
    protected override LocalizedString NewDisplayName
        => S["New service level policy"];

    /// <inheritdoc/>
    protected override LocalizedHtmlString CreatedNotification
        => H["The service level policy has been created."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString UpdatedNotification
        => H["The service level policy has been updated."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString DeletedNotification
        => H["The service level policy has been deleted."];

    /// <inheritdoc/>
    protected override void SetName(ServiceLevelPolicy entry, string name)
        => entry.Name = name;

    /// <inheritdoc/>
    protected override Task<bool> InitializeNewAsync(ServiceLevelPolicy model)
    {
        var requested = Request.Query["scope"].ToString();
        var scopes = _scopes.ToList();
        var scope = string.IsNullOrEmpty(requested) && scopes.Count == 1
            ? scopes[0]
            : scopes.FirstOrDefault(provider => string.Equals(provider.Name, requested, StringComparison.OrdinalIgnoreCase));

        if (scope is null)
        {
            return Task.FromResult(false);
        }

        model.Scope = scope.Name;

        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    /// <remarks>A policy that a record of another module still uses, such as a work order type, isn't deleted.</remarks>
    protected override async Task<bool> CanDeleteAsync(ServiceLevelPolicy model)
    {
        var usages = new List<string>();

        foreach (var provider in _usageProviders)
        {
            usages.AddRange(await provider.GetUsagesAsync(model, HttpContext.RequestAborted));
        }

        if (usages.Count == 0)
        {
            return true;
        }

        await Notifier.ErrorAsync(H["The policy can't be deleted while it is in use: {0}.", string.Join(", ", usages)]);

        return false;
    }

    /// <summary>
    /// Lists the policies.
    /// </summary>
    /// <param name="options">The list options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <param name="localizerFactory">The localizer factory, for the scopes' names.</param>
    /// <returns>The list view.</returns>
    [Admin(IndexRoute, IndexRouteName)]
    public Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory,
        [FromServices] IStringLocalizerFactory localizerFactory)
    {
        ViewData[nameof(ServiceLevelScopeOption)] = _scopes
            .Select(scope => new ServiceLevelScopeOption(scope.Name, ServiceLevelScopeNames.Get(localizerFactory, _scopes, scope.Name)))
            .OrderBy(scope => scope.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return IndexAsync(options, pagerParameters, pagerOptions, shapeFactory);
    }

    /// <summary>
    /// Applies the list search.
    /// </summary>
    /// <param name="model">The submitted list model.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <returns>A redirect to the filtered list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin(IndexRoute, IndexRouteName)]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
        => IndexFilterPostAsync(model, pagerParameters);

    /// <summary>
    /// Shows the form for a new policy of a scope, or for a copy of one.
    /// </summary>
    /// <param name="cloneId">The policy to copy, if any.</param>
    /// <returns>The create view.</returns>
    [Admin(CreateRoute, CreateRouteName)]
    public Task<IActionResult> Create(string cloneId = null)
        => CreateAsync(cloneId);

    /// <summary>
    /// Saves a new policy.
    /// </summary>
    /// <param name="cloneId">The policy the new one was copied from, if any.</param>
    /// <returns>A redirect to the list, or the form when it is not valid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin(CreateRoute, CreateRouteName)]
    public Task<IActionResult> CreatePost(string cloneId = null)
        => CreatePostAsync(cloneId);

    /// <summary>
    /// Shows the form of a policy.
    /// </summary>
    /// <param name="id">The policy id.</param>
    /// <returns>The edit view.</returns>
    [Admin("service-levels/edit/{id}", "ServiceLevelPoliciesEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    /// <summary>
    /// Saves a policy.
    /// </summary>
    /// <param name="id">The policy id.</param>
    /// <returns>A redirect to the list, or the form when it is not valid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("service-levels/edit/{id}", "ServiceLevelPoliciesEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    /// <summary>
    /// Creates a copy of a policy with every setting of the source, named "(copy)", and opens it in the editor.
    /// </summary>
    /// <param name="id">The policy to copy.</param>
    /// <returns>A redirect to the copy's editor, or to the list when the copy can't be created.</returns>
    [HttpPost]
    [Admin("service-levels/clone/{id}", "ServiceLevelPoliciesClone")]
    public Task<IActionResult> Clone(string id)
        => ClonePostAsync(id);

    /// <summary>
    /// Deletes a policy. Its timers that are still running keep their due times.
    /// </summary>
    /// <param name="id">The policy id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("service-levels/delete/{id}", "ServiceLevelPoliciesDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}
