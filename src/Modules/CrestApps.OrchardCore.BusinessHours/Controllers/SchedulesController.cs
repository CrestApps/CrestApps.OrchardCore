using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Core.Models;
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

namespace CrestApps.OrchardCore.BusinessHours.Controllers;

/// <summary>
/// Lists, creates, edits and deletes business hours schedules.
/// </summary>
[Admin]
public sealed class SchedulesController : CatalogAdminController<BusinessHoursSchedule>
{
    private const string IndexRoute = "business-hours";
    private const string IndexRouteName = "BusinessHoursSchedulesIndex";

    /// <summary>
    /// Initializes a new instance of the <see cref="SchedulesController"/> class.
    /// </summary>
    /// <param name="manager">The schedule manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SchedulesController(
        INamedCatalogManager<BusinessHoursSchedule> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<BusinessHoursSchedule> displayManager,
        INotifier notifier,
        IHtmlLocalizer<SchedulesController> htmlLocalizer,
        IStringLocalizer<SchedulesController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    /// <inheritdoc/>
    protected override Permission ManagePermission
        => BusinessHoursPermissions.ManageBusinessHoursSchedules;

    /// <inheritdoc/>
    protected override LocalizedString CreateDisplayName
        => S["Business hours schedule"];

    /// <inheritdoc/>
    protected override LocalizedString NewDisplayName
        => S["New business hours schedule"];

    /// <inheritdoc/>
    protected override LocalizedHtmlString CreatedNotification
        => H["The business hours schedule has been created."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString UpdatedNotification
        => H["The business hours schedule has been updated."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString DeletedNotification
        => H["The business hours schedule has been deleted."];

    /// <inheritdoc/>
    protected override void SetName(BusinessHoursSchedule entry, string name)
        => entry.Name = name;

    /// <summary>
    /// Lists the schedules.
    /// </summary>
    /// <param name="options">The list options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list view.</returns>
    [Admin(IndexRoute, IndexRouteName)]
    public Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
        => IndexAsync(options, pagerParameters, pagerOptions, shapeFactory);

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
    /// Shows the form for a new schedule, or for a copy of one.
    /// </summary>
    /// <param name="cloneId">The schedule to copy, if any.</param>
    /// <returns>The create view.</returns>
    [Admin("business-hours/create", "BusinessHoursSchedulesCreate")]
    public Task<IActionResult> Create(string cloneId = null)
        => CreateAsync(cloneId);

    /// <summary>
    /// Saves a new schedule.
    /// </summary>
    /// <param name="cloneId">The schedule the new one was copied from, if any.</param>
    /// <returns>A redirect to the list, or the form when it is not valid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("business-hours/create", "BusinessHoursSchedulesCreate")]
    public Task<IActionResult> CreatePost(string cloneId = null)
        => CreatePostAsync(cloneId);

    /// <summary>
    /// Shows the form of a schedule.
    /// </summary>
    /// <param name="id">The schedule id.</param>
    /// <returns>The edit view.</returns>
    [Admin("business-hours/edit/{id}", "BusinessHoursSchedulesEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    /// <summary>
    /// Saves a schedule.
    /// </summary>
    /// <param name="id">The schedule id.</param>
    /// <returns>A redirect to the list, or the form when it is not valid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("business-hours/edit/{id}", "BusinessHoursSchedulesEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    /// <summary>
    /// Creates a copy of a schedule with every setting of the source, named "(copy)", and opens it in the editor.
    /// </summary>
    /// <param name="id">The schedule to copy.</param>
    /// <returns>A redirect to the copy's editor, or to the list when the copy can't be created.</returns>
    [HttpPost]
    [Admin("business-hours/clone/{id}", "BusinessHoursSchedulesClone")]
    public Task<IActionResult> Clone(string id)
        => ClonePostAsync(id);

    /// <summary>
    /// Deletes a schedule.
    /// </summary>
    /// <param name="id">The schedule id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("business-hours/delete/{id}", "BusinessHoursSchedulesDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}
