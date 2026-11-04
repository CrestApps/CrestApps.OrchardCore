using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
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
/// Provides the administration screens for opportunity stage entries.
/// </summary>
[Admin]
public sealed class OpportunityStagesController : CatalogAdminController<OpportunityStage>
{
    private const string _nameFieldName = "Name";

    private readonly INamedCatalogManager<OpportunityStage> _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpportunityStagesController"/> class.
    /// </summary>
    /// <param name="manager">The manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OpportunityStagesController(
        INamedCatalogManager<OpportunityStage> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<OpportunityStage> displayManager,
        INotifier notifier,
        IHtmlLocalizer<OpportunityStagesController> htmlLocalizer,
        IStringLocalizer<OpportunityStagesController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
        _manager = manager;
    }

    /// <inheritdoc/>
    protected override Permission ManagePermission
        => OmnichannelConstants.Permissions.ManageOpportunityStages;

    /// <inheritdoc/>
    protected override LocalizedString CreateDisplayName
        => S["Opportunity Stage"];

    /// <inheritdoc/>
    protected override LocalizedString NewDisplayName
        => S["New Opportunity Stage"];

    /// <inheritdoc/>
    protected override LocalizedHtmlString CreatedNotification
        => H["The opportunity stage has been created successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString UpdatedNotification
        => H["The opportunity stage has been updated successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString DeletedNotification
        => H["The opportunity stage has been deleted successfully."];

    /// <inheritdoc/>
    protected override IEnumerable<OpportunityStage> OrderAll(IEnumerable<OpportunityStage> entries)
        => entries
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    protected override async Task ValidateEntryAsync(OpportunityStage model, bool isNew)
    {
        var existing = await _manager.FindByNameAsync(model.Name);

        if (existing is not null && !string.Equals(existing.ItemId, model.ItemId, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(_nameFieldName, S["A opportunity stage with the same name already exists."]);
        }
    }

    /// <summary>
    /// Lists every opportunity stage in order.
    /// </summary>
    /// <returns>The list view.</returns>
    [Admin("omnichannel/opportunity-stages", "OpportunityStagesIndex")]
    public Task<IActionResult> Index()
        => IndexAllAsync();

    /// <summary>
    /// Shows the editor for a new opportunity stage.
    /// </summary>
    /// <returns>The create view.</returns>
    [Admin("omnichannel/opportunity-stages/create", "OpportunityStagesCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    /// <summary>
    /// Creates a new opportunity stage.
    /// </summary>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("omnichannel/opportunity-stages/create", "OpportunityStagesCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    /// <summary>
    /// Shows the editor of an opportunity stage.
    /// </summary>
    /// <param name="id">The opportunity stage id.</param>
    /// <returns>The edit view.</returns>
    [Admin("omnichannel/opportunity-stages/edit/{id}", "OpportunityStagesEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    /// <summary>
    /// Updates an opportunity stage.
    /// </summary>
    /// <param name="id">The opportunity stage id.</param>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("omnichannel/opportunity-stages/edit/{id}", "OpportunityStagesEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    /// <summary>
    /// Deletes an opportunity stage.
    /// </summary>
    /// <param name="id">The opportunity stage id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [ActionName("Delete")]
    [Admin("omnichannel/opportunity-stages/delete/{id}", "OpportunityStagesDelete")]
    public Task<IActionResult> DeletePost(string id)
        => DeleteAsync(id);
}
