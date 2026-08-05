using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
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
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Provides administration of Contact Center business-hours calendars.
/// </summary>
[Admin]
[Feature(ContactCenterConstants.Feature.Queues)]
public sealed class BusinessHoursCalendarsController : ContactCenterCatalogController<BusinessHoursCalendar>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursCalendarsController"/> class.
    /// </summary>
    /// <param name="manager">The calendar manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BusinessHoursCalendarsController(
        IBusinessHoursCalendarManager manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<BusinessHoursCalendar> displayManager,
        INotifier notifier,
        IHtmlLocalizer<BusinessHoursCalendarsController> htmlLocalizer,
        IStringLocalizer<BusinessHoursCalendarsController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    /// <inheritdoc/>
    protected override Permission ManagePermission
        => ContactCenterPermissions.ManageQueues;

    /// <inheritdoc/>
    protected override LocalizedString CreateDisplayName
        => S["Business hours calendar"];

    /// <inheritdoc/>
    protected override LocalizedString NewDisplayName
        => S["New business hours calendar"];

    /// <inheritdoc/>
    protected override LocalizedHtmlString CreatedNotification
        => H["A new business hours calendar has been created successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString UpdatedNotification
        => H["The business hours calendar has been updated successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString DeletedNotification
        => H["The business hours calendar has been deleted successfully."];

    /// <summary>
    /// Lists the calendars.
    /// </summary>
    /// <param name="options">The catalog entry options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The calendars list view.</returns>
    [Admin("contact-center/business-hours", "ContactCenterBusinessHoursIndex")]
    public Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
        => IndexAsync(options, pagerParameters, pagerOptions, shapeFactory);

    /// <summary>
    /// Applies the calendars list filter.
    /// </summary>
    /// <param name="model">The submitted list model.</param>
    /// <returns>A redirect to the filtered list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("contact-center/business-hours", "ContactCenterBusinessHoursIndex")]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model)
        => IndexFilterPostAsync(model);

    /// <summary>
    /// Displays the calendar create form.
    /// </summary>
    /// <returns>The create view.</returns>
    [Admin("contact-center/business-hours/create", "ContactCenterBusinessHoursCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    /// <summary>
    /// Persists a new calendar.
    /// </summary>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("contact-center/business-hours/create", "ContactCenterBusinessHoursCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    /// <summary>
    /// Displays the calendar edit form.
    /// </summary>
    /// <param name="id">The calendar identifier.</param>
    /// <returns>The edit view.</returns>
    [Admin("contact-center/business-hours/edit/{id}", "ContactCenterBusinessHoursEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    /// <summary>
    /// Persists changes to a calendar.
    /// </summary>
    /// <param name="id">The calendar identifier.</param>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("contact-center/business-hours/edit/{id}", "ContactCenterBusinessHoursEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    /// <summary>
    /// Deletes a calendar.
    /// </summary>
    /// <param name="id">The calendar identifier.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("contact-center/business-hours/delete/{id}", "ContactCenterBusinessHoursDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}
