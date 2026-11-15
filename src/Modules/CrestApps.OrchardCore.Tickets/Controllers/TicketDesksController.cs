using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tickets.Controllers;

/// <summary>
/// The admin pages of desks.
/// </summary>
[Admin]
public sealed class TicketDesksController : CatalogAdminController<TicketDesk>
{
    private const string IndexRoute = "tickets/desks";
    private const string IndexRouteName = "TicketDesksIndex";

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketDesksController"/> class.
    /// </summary>
    /// <param name="manager">The records.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TicketDesksController(
        ICatalogManager<TicketDesk> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<TicketDesk> displayManager,
        INotifier notifier,
        IHtmlLocalizer<TicketDesksController> htmlLocalizer,
        IStringLocalizer<TicketDesksController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    /// <inheritdoc/>
    /// <inheritdoc/>
    protected override async Task<bool> CanDeleteAsync(TicketDesk model)
    {
        // A record tickets still use isn't deleted: they would point at nothing.
        var usages = await HttpContext.RequestServices.GetRequiredService<TicketSettingsUsage>().GetUsagesAsync(model, HttpContext.RequestAborted);

        if (usages.Count == 0)
        {
            return true;
        }

        await Notifier.ErrorAsync(H["{0} is still used by {1}, so it can't be deleted.", model.Name, string.Join(", ", usages)]);

        return false;
    }

    protected override Permission ManagePermission
        => TicketsPermissions.ManageTicketSettings;

    /// <inheritdoc/>
    protected override LocalizedString CreateDisplayName
        => S["Desk"];

    /// <inheritdoc/>
    protected override LocalizedString NewDisplayName
        => S["New desk"];

    /// <inheritdoc/>
    protected override LocalizedHtmlString CreatedNotification
        => H["The desk has been created."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString UpdatedNotification
        => H["The desk has been updated."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString DeletedNotification
        => H["The desk has been deleted."];

    /// <inheritdoc/>
    protected override void SetName(TicketDesk entry, string name)
        => entry.Name = name;

    /// <summary>
    /// Lists the records.
    /// </summary>
    /// <param name="options">The list options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list page.</returns>
    [Admin(IndexRoute, IndexRouteName)]
    public Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
        => IndexAsync(options, pagerParameters, pagerOptions, shapeFactory);

    /// <summary>
    /// Applies the list's search.
    /// </summary>
    /// <param name="model">The list.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin(IndexRoute, IndexRouteName)]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
        => IndexFilterPostAsync(model, pagerParameters);

    /// <summary>
    /// Shows the editor of a new record.
    /// </summary>
    /// <param name="cloneId">The record to copy, if any.</param>
    /// <returns>The editor.</returns>
    [Admin("tickets/desks/create", "TicketDesksCreate")]
    public Task<IActionResult> Create(string cloneId = null)
        => CreateAsync(cloneId);

    /// <summary>
    /// Creates a record.
    /// </summary>
    /// <param name="cloneId">The record copied, if any.</param>
    /// <returns>A redirect to the list, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("tickets/desks/create", "TicketDesksCreate")]
    public Task<IActionResult> CreatePost(string cloneId = null)
        => CreatePostAsync(cloneId);

    /// <summary>
    /// Shows the editor of a record.
    /// </summary>
    /// <param name="id">The record.</param>
    /// <returns>The editor.</returns>
    [Admin("tickets/desks/edit/{id}", "TicketDesksEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    /// <summary>
    /// Saves a record.
    /// </summary>
    /// <param name="id">The record.</param>
    /// <returns>A redirect to the list, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("tickets/desks/edit/{id}", "TicketDesksEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    /// <summary>
    /// Copies a record.
    /// </summary>
    /// <param name="id">The record.</param>
    /// <returns>A redirect to the copy's editor.</returns>
    [HttpPost]
    [Admin("tickets/desks/clone/{id}", "TicketDesksClone")]
    public Task<IActionResult> Clone(string id)
        => ClonePostAsync(id);

    /// <summary>
    /// Deletes a record.
    /// </summary>
    /// <param name="id">The record.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("tickets/desks/delete/{id}", "TicketDesksDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}
