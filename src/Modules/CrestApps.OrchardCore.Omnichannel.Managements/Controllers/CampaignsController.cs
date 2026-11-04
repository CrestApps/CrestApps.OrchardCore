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
/// Provides the administration screens for campaigns.
/// </summary>
[Admin]
public sealed class CampaignsController : CatalogAdminController<OmnichannelCampaign>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CampaignsController"/> class.
    /// </summary>
    /// <param name="manager">The manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CampaignsController(
        ICatalogManager<OmnichannelCampaign> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<OmnichannelCampaign> displayManager,
        INotifier notifier,
        IHtmlLocalizer<CampaignsController> htmlLocalizer,
        IStringLocalizer<CampaignsController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    /// <inheritdoc/>
    protected override Permission ManagePermission
        => OmnichannelConstants.Permissions.ManageCampaigns;

    /// <inheritdoc/>
    protected override LocalizedString CreateDisplayName
        => S["Campaign"];

    /// <inheritdoc/>
    protected override LocalizedString NewDisplayName
        => S["New Campaign"];

    /// <inheritdoc/>
    protected override LocalizedHtmlString CreatedNotification
        => H["A new Campaign has been created successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString UpdatedNotification
        => H["The Campaign has been updated successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString DeletedNotification
        => H["The Campaign has been deleted successfully."];

    /// <summary>
    /// Lists the campaigns.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list view.</returns>
    [Admin("omnichannel/campaigns", "OmnichannelCampaignsIndex")]
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
    [Admin("omnichannel/campaigns", "OmnichannelCampaignsIndex")]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
        => IndexFilterPostAsync(model, pagerParameters);

    /// <summary>
    /// Shows the editor for a new campaign.
    /// </summary>
    /// <returns>The create view.</returns>
    [Admin("omnichannel/campaigns/create", "OmnichannelCampaignsCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    /// <summary>
    /// Creates a new campaign.
    /// </summary>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("omnichannel/campaigns/create", "OmnichannelCampaignsCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    /// <summary>
    /// Shows the editor of a campaign.
    /// </summary>
    /// <param name="id">The campaign id.</param>
    /// <returns>The edit view.</returns>
    [Admin("omnichannel/campaigns/edit/{id}", "OmnichannelCampaignsEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    /// <summary>
    /// Updates a campaign.
    /// </summary>
    /// <param name="id">The campaign id.</param>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("omnichannel/campaigns/edit/{id}", "OmnichannelCampaignsEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);
}