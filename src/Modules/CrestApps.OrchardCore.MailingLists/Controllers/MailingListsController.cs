using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.MailingLists.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Security.Permissions;
using QueryContext = CrestApps.Core.Models.QueryContext;

namespace CrestApps.OrchardCore.MailingLists.Controllers;

/// <summary>
/// The admin pages of mailing lists: the list of lists, which anyone who may see members reads, and the editor, which
/// other features extend with their own cards (the welcome coupon, a provider's settings).
/// </summary>
[Admin]
public sealed class MailingListsController : CatalogAdminController<MailingList>
{
    private const string IndexRoute = "mailing-lists";
    private const string IndexRouteName = "MailingListsIndex";

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListsController"/> class.
    /// </summary>
    public MailingListsController(
        ICatalogManager<MailingList> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<MailingList> displayManager,
        INotifier notifier,
        IHtmlLocalizer<MailingListsController> htmlLocalizer,
        IStringLocalizer<MailingListsController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    /// <inheritdoc/>
    protected override Permission ManagePermission
        => MailingListsPermissions.ManageMailingLists;

    /// <inheritdoc/>
    protected override LocalizedString CreateDisplayName
        => S["Mailing list"];

    /// <inheritdoc/>
    protected override LocalizedString NewDisplayName
        => S["New mailing list"];

    /// <inheritdoc/>
    protected override LocalizedHtmlString CreatedNotification
        => H["The mailing list has been created."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString UpdatedNotification
        => H["The mailing list has been updated."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString DeletedNotification
        => H["The mailing list has been deleted."];

    /// <inheritdoc/>
    protected override string GetName(MailingList entry)
        => string.IsNullOrWhiteSpace(entry?.DisplayText) ? entry?.Name : entry.DisplayText;

    /// <inheritdoc/>
    protected override void SetName(MailingList entry, string name)
        => entry.Name = name;

    /// <summary>
    /// Lists the mailing lists a page at a time.
    /// </summary>
    [Admin(IndexRoute, IndexRouteName)]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await AuthorizationService.AuthorizeAsync(User, MailingListsPermissions.ViewMailingListMembers))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var result = await Manager.PageAsync(pager.Page, pager.PageSize, new QueryContext
        {
            Name = options.Search,
        });

        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd("Options.Search", options.Search);
        }

        var pagerShape = await shapeFactory.PagerAsync(pager, result.Count, routeData);
        var entries = result.Entries.ToList();

        return View(new ListCatalogEntryViewModel<MailingList>
        {
            Models = entries,
            Options = options,
            Pager = pagerShape,
            List = new AdminListContext<MailingList>
            {
                Source = entries,
                Pager = pagerShape,
            },
        });
    }

    /// <summary>
    /// Applies the list's search.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin(IndexRoute, IndexRouteName)]
    public async Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
    {
        if (!await AuthorizationService.AuthorizeAsync(User, MailingListsPermissions.ViewMailingListMembers))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { "Options.Search", model.Options?.Search },
            { "pageSize", pagerParameters.PageSize },
        });
    }

    /// <summary>
    /// Shows the editor of a new mailing list.
    /// </summary>
    [Admin("mailing-lists/create", "MailingListsCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    /// <summary>
    /// Saves a new mailing list.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("mailing-lists/create", "MailingListsCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    /// <summary>
    /// Shows the editor of a mailing list.
    /// </summary>
    [Admin("mailing-lists/edit/{id}", "MailingListsEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    /// <summary>
    /// Saves a mailing list.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("mailing-lists/edit/{id}", "MailingListsEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    /// <summary>
    /// Deletes a mailing list. Its members stay until they are erased, so a list deleted by mistake can be made again
    /// from a recipe without losing the evidence of consent.
    /// </summary>
    [HttpPost]
    [Admin("mailing-lists/delete/{id}", "MailingListsDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}
