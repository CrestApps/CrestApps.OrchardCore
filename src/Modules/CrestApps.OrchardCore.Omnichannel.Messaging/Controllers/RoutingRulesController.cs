using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Controllers;

/// <summary>
/// Manages the messaging routing rules.
/// </summary>
[Admin]
[Feature(MessagingConstants.Feature.RoutingRules)]
public sealed class RoutingRulesController : CatalogAdminController<MessagingRoutingRule>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RoutingRulesController"/> class.
    /// </summary>
    /// <param name="manager">The routing rules.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RoutingRulesController(
        ICatalogManager<MessagingRoutingRule> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<MessagingRoutingRule> displayManager,
        INotifier notifier,
        IHtmlLocalizer<RoutingRulesController> htmlLocalizer,
        IStringLocalizer<RoutingRulesController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    /// <inheritdoc/>
    protected override Permission ManagePermission
        => MessagingPermissions.ManageRoutingRules;

    /// <inheritdoc/>
    protected override LocalizedString CreateDisplayName
        => S["Routing Rule"];

    /// <inheritdoc/>
    protected override LocalizedString NewDisplayName
        => S["New Routing Rule"];

    /// <inheritdoc/>
    protected override LocalizedHtmlString CreatedNotification
        => H["A new routing rule has been created successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString UpdatedNotification
        => H["The routing rule has been updated successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString DeletedNotification
        => H["The routing rule has been deleted successfully."];

    /// <inheritdoc/>
    protected override IEnumerable<MessagingRoutingRule> OrderAll(IEnumerable<MessagingRoutingRule> entries)
        => entries
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.DisplayText, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Lists the routing rules in the order they are tried.
    /// </summary>
    /// <returns>The list view.</returns>
    [Admin("messaging/routing-rules", "MessagingRoutingRulesIndex")]
    public Task<IActionResult> Index()
        => IndexAllAsync();

    /// <summary>
    /// Shows the editor for a new routing rule.
    /// </summary>
    /// <returns>The create view.</returns>
    [Admin("messaging/routing-rules/create", "MessagingRoutingRulesCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    /// <summary>
    /// Saves a new routing rule.
    /// </summary>
    /// <returns>A redirect to the list, or the form when it is not valid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("messaging/routing-rules/create", "MessagingRoutingRulesCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    /// <summary>
    /// Shows the editor of a routing rule.
    /// </summary>
    /// <param name="id">The rule's identifier.</param>
    /// <returns>The edit view.</returns>
    [Admin("messaging/routing-rules/edit/{id}", "MessagingRoutingRulesEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    /// <summary>
    /// Saves a routing rule.
    /// </summary>
    /// <param name="id">The rule's identifier.</param>
    /// <returns>A redirect to the list, or the form when it is not valid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("messaging/routing-rules/edit/{id}", "MessagingRoutingRulesEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    /// <summary>
    /// Deletes a routing rule.
    /// </summary>
    /// <param name="id">The rule's identifier.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("messaging/routing-rules/delete/{id}", "MessagingRoutingRulesDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}
