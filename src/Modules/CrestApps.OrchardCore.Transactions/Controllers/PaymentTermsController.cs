using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
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

namespace CrestApps.OrchardCore.Transactions.Controllers;

/// <summary>
/// Lists, creates, changes and deletes the store's payment terms.
/// </summary>
[Admin]
[Feature(TransactionsConstants.Features.PaymentTerms)]
public sealed class PaymentTermsController : CatalogAdminController<PaymentTerm>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentTermsController"/> class.
    /// </summary>
    /// <param name="manager">The payment term manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The model updater accessor.</param>
    /// <param name="displayManager">The display manager of the terms.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PaymentTermsController(
        IPaymentTermManager manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<PaymentTerm> displayManager,
        INotifier notifier,
        IHtmlLocalizer<PaymentTermsController> htmlLocalizer,
        IStringLocalizer<PaymentTermsController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    /// <inheritdoc/>
    protected override Permission ManagePermission
        => TransactionsPermissions.ManagePaymentTerms;

    /// <inheritdoc/>
    protected override LocalizedString CreateDisplayName
        => S["Payment term"];

    /// <inheritdoc/>
    protected override LocalizedString NewDisplayName
        => S["New payment term"];

    /// <inheritdoc/>
    protected override LocalizedHtmlString CreatedNotification
        => H["The payment term has been added."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString UpdatedNotification
        => H["The payment term has been updated. Documents already issued keep the due date they were given."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString DeletedNotification
        => H["The payment term has been deleted. Documents already issued keep its name and due date."];

    /// <summary>
    /// Lists the terms.
    /// </summary>
    /// <param name="options">The search options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    [Admin("commerce/payment-terms", "PaymentTermsIndex")]
    public Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
        => IndexAsync(options, pagerParameters, pagerOptions, shapeFactory);

    /// <summary>
    /// Applies the search.
    /// </summary>
    /// <param name="model">The list.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("commerce/payment-terms", "PaymentTermsIndex")]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
        => IndexFilterPostAsync(model, pagerParameters);

    /// <summary>
    /// Shows the editor of a new term.
    /// </summary>
    [Admin("commerce/payment-terms/create", "PaymentTermsCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    /// <summary>
    /// Saves a new term.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("commerce/payment-terms/create", "PaymentTermsCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    /// <summary>
    /// Shows the editor of a term.
    /// </summary>
    /// <param name="id">The term's id.</param>
    [Admin("commerce/payment-terms/edit/{id}", "PaymentTermsEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    /// <summary>
    /// Saves a term.
    /// </summary>
    /// <param name="id">The term's id.</param>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("commerce/payment-terms/edit/{id}", "PaymentTermsEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    /// <summary>
    /// Deletes a term.
    /// </summary>
    /// <param name="id">The term's id.</param>
    [HttpPost]
    [Admin("commerce/payment-terms/delete/{id}", "PaymentTermsDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}
