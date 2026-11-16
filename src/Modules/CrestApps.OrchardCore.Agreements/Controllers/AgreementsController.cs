using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.Core.Services;
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
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Agreements.Controllers;

/// <summary>
/// The admin pages of agreements: the list and the editor, which other modules extend with their own terms.
/// </summary>
[Admin]
public sealed class AgreementsController : CatalogAdminController<Agreement>
{
    private const string IndexRoute = "agreements";
    private const string IndexRouteName = "AgreementsIndex";

    private readonly ILocalClock _localClock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgreementsController"/> class.
    /// </summary>
    public AgreementsController(
        ICatalogManager<Agreement> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<Agreement> displayManager,
        INotifier notifier,
        ILocalClock localClock,
        IHtmlLocalizer<AgreementsController> htmlLocalizer,
        IStringLocalizer<AgreementsController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
        _localClock = localClock;
    }

    /// <inheritdoc/>
    protected override Permission ManagePermission
        => AgreementsPermissions.ManageAgreements;

    /// <inheritdoc/>
    protected override LocalizedString CreateDisplayName
        => S["Agreement"];

    /// <inheritdoc/>
    protected override LocalizedString NewDisplayName
        => S["New agreement"];

    /// <inheritdoc/>
    protected override LocalizedHtmlString CreatedNotification
        => H["The agreement has been created."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString UpdatedNotification
        => H["The agreement has been updated."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString DeletedNotification
        => H["The agreement has been deleted."];

    /// <inheritdoc/>
    protected override string GetName(Agreement entry)
        => string.IsNullOrEmpty(entry.Number) ? entry.Name : entry.Number + " " + entry.Name;

    /// <inheritdoc/>
    protected override void SetName(Agreement entry, string name)
        => entry.Name = name;

    /// <inheritdoc/>
    protected override async Task<bool> InitializeNewAsync(Agreement model)
    {
        // The time zone the user sees times in: theirs, the site's, or the server's when neither is set.
        model.TimeZoneId ??= (await _localClock.GetLocalTimeZoneAsync())?.TimeZoneId;

        if (Request.Query.TryGetValue("accountId", out var accountId) && !string.IsNullOrWhiteSpace(accountId))
        {
            model.AccountId = accountId.ToString().Trim();
        }

        return true;
    }

    /// <inheritdoc/>
    protected override void InitializeClone(Agreement clone, Agreement source)
    {
        clone.Number = null;
        clone.Status = AgreementStatus.Draft;
        clone.ExpiringNotifiedUtc = null;
        clone.HoursWarnings = new AgreementHoursWarnings();
    }

    /// <inheritdoc/>
    protected override async Task<bool> CanDeleteAsync(Agreement model)
    {
        if (model.Status == AgreementStatus.Draft)
        {
            return true;
        }

        await Notifier.WarningAsync(H["Only a draft agreement can be deleted. Cancel {0} instead.", model.Number]);

        return false;
    }

    /// <summary>
    /// Lists the agreements.
    /// </summary>
    [Admin(IndexRoute, IndexRouteName)]
    public Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
        => IndexAsync(options, pagerParameters, pagerOptions, shapeFactory);

    /// <summary>
    /// Applies the list's search.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin(IndexRoute, IndexRouteName)]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
        => IndexFilterPostAsync(model, pagerParameters);

    /// <summary>
    /// Shows the editor of a new agreement.
    /// </summary>
    [Admin("agreements/create", "AgreementsCreate")]
    public Task<IActionResult> Create(string cloneId = null)
        => CreateAsync(cloneId);

    /// <summary>
    /// Saves a new agreement.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("agreements/create", "AgreementsCreate")]
    public Task<IActionResult> CreatePost(string cloneId = null)
        => CreatePostAsync(cloneId);

    /// <summary>
    /// Shows the editor of an agreement.
    /// </summary>
    [Admin("agreements/edit/{id}", "AgreementsEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    /// <summary>
    /// Saves an agreement.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("agreements/edit/{id}", "AgreementsEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    /// <summary>
    /// Copies an agreement as a draft.
    /// </summary>
    [HttpPost]
    [Admin("agreements/clone/{id}", "AgreementsClone")]
    public Task<IActionResult> Clone(string id)
        => ClonePostAsync(id);

    /// <summary>
    /// Deletes a draft agreement.
    /// </summary>
    [HttpPost]
    [Admin("agreements/delete/{id}", "AgreementsDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}
