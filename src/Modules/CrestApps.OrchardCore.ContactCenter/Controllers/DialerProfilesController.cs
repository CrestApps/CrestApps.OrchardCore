using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
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
[Feature(ContactCenterConstants.Feature.Dialer)]
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

    /// <inheritdoc/>
    protected override LocalizedHtmlString ClonedNotification
        => H["A copy of the dialer profile has been created. Review its settings before using it in a campaign."];

    /// <inheritdoc/>
    /// <remarks>
    /// A Predictive connect wait is only allowed once the profile's own connected calls show the wait still connects
    /// an agent in time. The copy has dialed nothing yet, so it starts without a connect wait; keeping it would refuse
    /// the copy outright.
    /// </remarks>
    protected override void InitializeClone(DialerProfile clone, DialerProfile source)
    {
        if (RequiresConnectWaitReset(source))
        {
            clone.ConnectWaitMilliseconds = 0;
        }
    }

    /// <inheritdoc/>
    protected override async Task OnClonedAsync(DialerProfile clone, DialerProfile source)
    {
        if (RequiresConnectWaitReset(source))
        {
            await Notifier.InformationAsync(H["The copy starts with no connect wait because it has not measured any connected calls yet. Set it again once the copy has dialed enough calls."]);
        }
    }

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
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <returns>A redirect to the filtered list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("contact-center/dialers", "ContactCenterDialersIndex")]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
        => IndexFilterPostAsync(model, pagerParameters);

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

    /// <summary>
    /// Creates a copy of a dialer profile with every setting of the source and opens it in the editor.
    /// </summary>
    /// <param name="id">The identifier of the dialer profile to copy.</param>
    /// <returns>A redirect to the copy's editor, or to the list when the copy cannot be created.</returns>
    [HttpPost]
    [Admin("contact-center/dialers/clone/{id}", "ContactCenterDialersClone")]
    public Task<IActionResult> Clone(string id)
        => ClonePostAsync(id);

    private static bool RequiresConnectWaitReset(DialerProfile source)
        => source.Mode == DialerMode.Predictive && source.ConnectWaitMilliseconds > 0;
}
