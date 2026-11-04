using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Controllers;

/// <summary>
/// Provides the administration screens for lead status entries.
/// </summary>
[Admin]
public sealed class LeadStatusesController : CatalogAdminController<LeadStatus>
{
    private const string _nameFieldName = "Name";

    private readonly INamedCatalogManager<LeadStatus> _manager;
    private readonly LeadStatusFlagService _flagService;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadStatusesController"/> class.
    /// </summary>
    /// <param name="manager">The manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="flagService">Keeps the single-status flags on one status.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LeadStatusesController(
        INamedCatalogManager<LeadStatus> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<LeadStatus> displayManager,
        INotifier notifier,
        LeadStatusFlagService flagService,
        IHtmlLocalizer<LeadStatusesController> htmlLocalizer,
        IStringLocalizer<LeadStatusesController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
        _manager = manager;
        _flagService = flagService;
    }

    /// <inheritdoc/>
    protected override Permission ManagePermission
        => OmnichannelConstants.Permissions.ManageLeadStatuses;

    /// <inheritdoc/>
    protected override LocalizedString CreateDisplayName
        => S["Lead Status"];

    /// <inheritdoc/>
    protected override LocalizedString NewDisplayName
        => S["New Lead Status"];

    /// <inheritdoc/>
    protected override LocalizedHtmlString CreatedNotification
        => H["The lead status has been created successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString UpdatedNotification
        => H["The lead status has been updated successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString DeletedNotification
        => H["The lead status has been deleted successfully."];

    /// <inheritdoc/>
    protected override IEnumerable<LeadStatus> OrderAll(IEnumerable<LeadStatus> entries)
        => entries
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    protected override async Task ValidateEntryAsync(LeadStatus model, bool isNew)
    {
        var existing = await _manager.FindByNameAsync(model.Name);

        if (existing is not null && !string.Equals(existing.ItemId, model.ItemId, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(_nameFieldName, S["A lead status with the same name already exists."]);
        }
    }

    /// <inheritdoc/>
    protected override Task OnSavedAsync(LeadStatus model, bool isNew)
        => _flagService.EnforceAsync(model);

    /// <inheritdoc/>
    protected override async Task<bool> CanDeleteAsync(LeadStatus model)
    {
        if (!model.IsConverted)
        {
            return true;
        }

        await Notifier.ErrorAsync(H["The converted status cannot be deleted, because converting a lead needs it. Mark another status as the converted status first."]);

        return false;
    }

    /// <summary>
    /// Lists every lead status in order.
    /// </summary>
    /// <returns>The list view.</returns>
    [Admin("omnichannel/lead-statuses", "LeadStatusesIndex")]
    public Task<IActionResult> Index()
        => IndexAllAsync();

    /// <summary>
    /// Shows the editor for a new lead status.
    /// </summary>
    /// <returns>The create view.</returns>
    [Admin("omnichannel/lead-statuses/create", "LeadStatusesCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    /// <summary>
    /// Creates a new lead status.
    /// </summary>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("omnichannel/lead-statuses/create", "LeadStatusesCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    /// <summary>
    /// Shows the editor of a lead status.
    /// </summary>
    /// <param name="id">The lead status id.</param>
    /// <returns>The edit view.</returns>
    [Admin("omnichannel/lead-statuses/edit/{id}", "LeadStatusesEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    /// <summary>
    /// Updates a lead status.
    /// </summary>
    /// <param name="id">The lead status id.</param>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("omnichannel/lead-statuses/edit/{id}", "LeadStatusesEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    /// <summary>
    /// Deletes a lead status.
    /// </summary>
    /// <param name="id">The lead status id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [ActionName("Delete")]
    [Admin("omnichannel/lead-statuses/delete/{id}", "LeadStatusesDelete")]
    public Task<IActionResult> DeletePost(string id)
        => DeleteAsync(id);
}
