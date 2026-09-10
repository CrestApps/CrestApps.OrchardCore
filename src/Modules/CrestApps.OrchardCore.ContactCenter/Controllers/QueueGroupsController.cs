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
/// Provides administration of Contact Center queue groups.
/// </summary>
[Admin]
[Feature(ContactCenterConstants.Feature.Queues)]
public sealed class QueueGroupsController : ContactCenterCatalogController<ActivityQueueGroup>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueueGroupsController"/> class.
    /// </summary>
    /// <param name="manager">The queue-group manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public QueueGroupsController(
        IActivityQueueGroupManager manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<ActivityQueueGroup> displayManager,
        INotifier notifier,
        IHtmlLocalizer<QueueGroupsController> htmlLocalizer,
        IStringLocalizer<QueueGroupsController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    /// <inheritdoc/>
    protected override Permission ManagePermission
        => ContactCenterPermissions.ManageQueueGroups;

    /// <inheritdoc/>
    protected override LocalizedString CreateDisplayName
        => S["Queue group"];

    /// <inheritdoc/>
    protected override LocalizedString NewDisplayName
        => S["New queue group"];

    /// <inheritdoc/>
    protected override LocalizedHtmlString CreatedNotification
        => H["A new queue group has been created successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString UpdatedNotification
        => H["The queue group has been updated successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString DeletedNotification
        => H["The queue group has been deleted successfully."];

    /// <summary>
    /// Lists the queue groups.
    /// </summary>
    /// <param name="options">The catalog entry options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The queue groups list view.</returns>
    [Admin("contact-center/queue-groups", "ContactCenterQueueGroupsIndex")]
    public Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
        => IndexAsync(options, pagerParameters, pagerOptions, shapeFactory);

    /// <summary>
    /// Applies the queue groups list filter.
    /// </summary>
    /// <param name="model">The submitted list model.</param>
    /// <returns>A redirect to the filtered list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("contact-center/queue-groups", "ContactCenterQueueGroupsIndex")]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model)
        => IndexFilterPostAsync(model);

    /// <summary>
    /// Displays the queue group create form.
    /// </summary>
    /// <returns>The create view.</returns>
    [Admin("contact-center/queue-groups/create", "ContactCenterQueueGroupsCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    /// <summary>
    /// Persists a new queue group.
    /// </summary>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("contact-center/queue-groups/create", "ContactCenterQueueGroupsCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    /// <summary>
    /// Displays the queue group edit form.
    /// </summary>
    /// <param name="id">The queue group identifier.</param>
    /// <returns>The edit view.</returns>
    [Admin("contact-center/queue-groups/edit/{id}", "ContactCenterQueueGroupsEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    /// <summary>
    /// Persists changes to a queue group.
    /// </summary>
    /// <param name="id">The queue group identifier.</param>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("contact-center/queue-groups/edit/{id}", "ContactCenterQueueGroupsEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    /// <summary>
    /// Deletes a queue group.
    /// </summary>
    /// <param name="id">The queue group identifier.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("contact-center/queue-groups/delete/{id}", "ContactCenterQueueGroupsDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}
