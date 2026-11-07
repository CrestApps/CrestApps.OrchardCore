using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
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

namespace CrestApps.OrchardCore.Omnichannel.Managements.Controllers;

/// <summary>
/// Provides the administration screens for dispositions.
/// </summary>
[Admin]
public sealed class DispositionsController : CatalogAdminController<OmnichannelDisposition>
{
    private const string _uniqueFieldName = "DisplayText";

    private readonly INamedCatalogManager<OmnichannelDisposition> _manager;
    /// <summary>
    /// Initializes a new instance of the <see cref="DispositionsController"/> class.
    /// </summary>
    /// <param name="manager">The manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DispositionsController(
        INamedCatalogManager<OmnichannelDisposition> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<OmnichannelDisposition> displayManager,
        INotifier notifier,
        IHtmlLocalizer<DispositionsController> htmlLocalizer,
        IStringLocalizer<DispositionsController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
        _manager = manager;
    }

    /// <inheritdoc/>
    protected override Permission ManagePermission
        => OmnichannelConstants.Permissions.ManageDispositions;

    /// <inheritdoc/>
    protected override LocalizedString CreateDisplayName
        => S["Disposition"];

    /// <inheritdoc/>
    protected override LocalizedString NewDisplayName
        => S["New Disposition"];

    /// <inheritdoc/>
    protected override LocalizedHtmlString CreatedNotification
        => H["A new Disposition has been created successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString UpdatedNotification
        => H["The Disposition has been updated successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString DeletedNotification
        => H["The Disposition has been deleted successfully."];

    /// <inheritdoc/>
    protected override async Task ValidateEntryAsync(OmnichannelDisposition model, bool isNew)
    {
        var existing = await _manager.FindByNameAsync(model.Name);

        if (existing is not null && !string.Equals(existing.ItemId, model.ItemId, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(_uniqueFieldName, S["A disposition with the same name already exists."]);
        }
    }
    /// <summary>
    /// Lists the dispositions.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list view.</returns>
    [Admin("omnichannel/dispositions", "OmnichannelDispositionsIndex")]
    public Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
        => IndexAsync(options, pagerParameters, pagerOptions, shapeFactory);

    /// <summary>
    /// Applies the list filter.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <returns>A redirect to the filtered list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("omnichannel/dispositions", "OmnichannelDispositionsIndex")]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
        => IndexFilterPostAsync(model, pagerParameters);

    /// <summary>
    /// Shows the editor for a new disposition.
    /// </summary>
    /// <returns>The create view.</returns>
    [Admin("omnichannel/dispositions/create", "OmnichannelDispositionsCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    /// <summary>
    /// Creates a new disposition.
    /// </summary>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("omnichannel/dispositions/create", "OmnichannelDispositionsCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    /// <summary>
    /// Shows the editor of a disposition.
    /// </summary>
    /// <param name="id">The disposition id.</param>
    /// <returns>The edit view.</returns>
    [Admin("omnichannel/dispositions/edit/{id}", "OmnichannelDispositionsEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    /// <summary>
    /// Updates a disposition.
    /// </summary>
    /// <param name="id">The disposition id.</param>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("omnichannel/dispositions/edit/{id}", "OmnichannelDispositionsEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    /// <summary>
    /// Deletes a disposition.
    /// </summary>
    /// <param name="id">The disposition id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [ActionName("Delete")]
    [Admin("omnichannel/dispositions/delete/{id}", "OmnichannelDispositionsDelete")]
    public Task<IActionResult> DeletePost(string id)
        => DeleteAsync(id);}