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
/// Provides administration of dialer profiles.
/// </summary>
[Admin]
[Feature(ContactCenterConstants.Feature.DialerAdmin)]
public sealed class DialerProfilesController : ContactCenterCatalogController<DialerProfile>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DialerProfilesController"/> class.
    /// </summary>
    /// <param name="manager">The dialer profile manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialerProfilesController(
        IDialerProfileManager manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<DialerProfile> displayManager,
        INotifier notifier,
        IHtmlLocalizer<DialerProfilesController> htmlLocalizer,
        IStringLocalizer<DialerProfilesController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    /// <inheritdoc/>
    protected override Permission ManagePermission
        => ContactCenterPermissions.ManageDialer;

    /// <inheritdoc/>
    protected override LocalizedString CreateDisplayName
        => S["Dialer Profile"];

    /// <inheritdoc/>
    protected override LocalizedString NewDisplayName
        => S["New Dialer Profile"];

    /// <inheritdoc/>
    protected override LocalizedHtmlString CreatedNotification
        => H["A new dialer profile has been created successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString UpdatedNotification
        => H["The dialer profile has been updated successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString DeletedNotification
        => H["The dialer profile has been deleted successfully."];

    /// <summary>
    /// Lists the dialer profiles.
    /// </summary>
    /// <param name="options">The catalog entry options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The dialer profiles list view.</returns>
    [Admin("contact-center/dialers", "ContactCenterDialersIndex")]
    public Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
        => IndexAsync(options, pagerParameters, pagerOptions, shapeFactory);

    /// <summary>
    /// Applies the dialer profiles list filter.
    /// </summary>
    /// <param name="model">The submitted list model.</param>
    /// <returns>A redirect to the filtered list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("contact-center/dialers", "ContactCenterDialersIndex")]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model)
        => IndexFilterPostAsync(model);

    /// <summary>
    /// Displays the dialer profile create form.
    /// </summary>
    /// <returns>The create view.</returns>
    [Admin("contact-center/dialers/create", "ContactCenterDialersCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    /// <summary>
    /// Persists a new dialer profile.
    /// </summary>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("contact-center/dialers/create", "ContactCenterDialersCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    /// <summary>
    /// Displays the dialer profile edit form.
    /// </summary>
    /// <param name="id">The dialer profile identifier.</param>
    /// <returns>The edit view.</returns>
    [Admin("contact-center/dialers/edit/{id}", "ContactCenterDialersEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    /// <summary>
    /// Persists changes to a dialer profile.
    /// </summary>
    /// <param name="id">The dialer profile identifier.</param>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("contact-center/dialers/edit/{id}", "ContactCenterDialersEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    /// <summary>
    /// Deletes a dialer profile.
    /// </summary>
    /// <param name="id">The dialer profile identifier.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("contact-center/dialers/delete/{id}", "ContactCenterDialersDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}
