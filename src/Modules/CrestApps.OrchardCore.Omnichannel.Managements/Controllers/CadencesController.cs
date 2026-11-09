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
/// Provides the administration screens for cadences.
/// </summary>
[Admin]
public sealed class CadencesController : CatalogAdminController<Cadence>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CadencesController"/> class.
    /// </summary>
    /// <param name="manager">The manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CadencesController(
        ICatalogManager<Cadence> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<Cadence> displayManager,
        INotifier notifier,
        IHtmlLocalizer<CadencesController> htmlLocalizer,
        IStringLocalizer<CadencesController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    /// <inheritdoc/>
    protected override Permission ManagePermission
        => OmnichannelConstants.Permissions.ManageCadences;

    /// <inheritdoc/>
    protected override LocalizedString CreateDisplayName
        => S["Cadence"];

    /// <inheritdoc/>
    protected override LocalizedString NewDisplayName
        => S["New Cadence"];

    /// <inheritdoc/>
    protected override LocalizedHtmlString CreatedNotification
        => H["A new cadence has been created successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString UpdatedNotification
        => H["The cadence has been updated successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString DeletedNotification
        => H["The cadence has been deleted successfully."];

    /// <summary>
    /// Lists the cadences.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list view.</returns>
    [Admin("omnichannel/cadences", "OmnichannelCadencesIndex")]
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
    [Admin("omnichannel/cadences", "OmnichannelCadencesIndex")]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
        => IndexFilterPostAsync(model, pagerParameters);

    /// <summary>
    /// Shows the editor for a new cadence.
    /// </summary>
    /// <returns>The create view.</returns>
    [Admin("omnichannel/cadences/create", "OmnichannelCadencesCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    /// <summary>
    /// Creates a new cadence.
    /// </summary>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("omnichannel/cadences/create", "OmnichannelCadencesCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    /// <summary>
    /// Shows the editor of a cadence.
    /// </summary>
    /// <param name="id">The cadence id.</param>
    /// <returns>The edit view.</returns>
    [Admin("omnichannel/cadences/edit/{id}", "OmnichannelCadencesEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    /// <summary>
    /// Updates a cadence.
    /// </summary>
    /// <param name="id">The cadence id.</param>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("omnichannel/cadences/edit/{id}", "OmnichannelCadencesEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    /// <summary>
    /// Deletes a cadence.
    /// </summary>
    /// <param name="id">The cadence id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [ActionName("Delete")]
    [Admin("omnichannel/cadences/delete/{id}", "OmnichannelCadencesDelete")]
    public Task<IActionResult> DeletePost(string id)
        => DeleteAsync(id);}