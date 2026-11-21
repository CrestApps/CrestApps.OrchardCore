using CrestApps.Core.Services;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.BusinessDocuments;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Migrations;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Routing;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Invoicing.Collections;

/// <summary>
/// Registers collections: the late-fee policies with their admin pages and index, the late-fee task, and the late fees
/// shown on the invoice's page.
/// </summary>
[Feature(InvoicingConstants.Features.Collections)]
public sealed class CollectionsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        // With the recipe step below, so an imported policy gets the editor's rules.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<LateFeePolicy>, LateFeePolicyHandler>());

        services
            .AddInvoicingCollections()
            .AddIndexProvider<LateFeePolicyIndexProvider>()
            .AddDataMigration<LateFeePolicyMigrations>()
            .AddRecipeExecutionStep<LateFeePoliciesStep>()
            .AddDisplayDriver<LateFeePolicy, LateFeePolicyDisplayDriver>()
            .AddDisplayDriver<Invoice, InvoiceLateFeesDisplayDriver>()
            .AddNavigationProvider<LateFeePoliciesAdminMenu>()
            .AddBusinessDocumentKind(InvoicingConstants.BusinessDocumentKinds.Statement, LocalizationSource.Create<CollectionsStartup>("Customer statements"))
            .AddCommerceEmailKind(
                InvoicingConstants.EmailKinds.Statement,
                LocalizationSource.Create<CollectionsStartup>("Statement e-mailed to a customer"),
                LocalizationSource.Create<CollectionsStartup>("{{ CustomerName }}, {{ BalanceDue }} and {{ StoreName }}. The statement's PDF is attached."));

        services.AddSingleton<IBackgroundTask, LateFeeBackgroundTask>();
    }
}

/// <summary>
/// Charges the late fees due on overdue invoices, on the site's date.
/// </summary>
[BackgroundTask(
    Title = "Invoice Late Fees",
    Schedule = "15 * * * *",
    Description = "Charges the late fees due on overdue invoices as debit notes, under the late-fee policies.",
    LockTimeout = 5_000,
    LockExpiration = 600_000)]
public sealed class LateFeeBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run =>
        {
            var localClock = serviceProvider.GetRequiredService<ILocalClock>();
            var today = DateOnly.FromDateTime((await localClock.GetLocalNowAsync()).DateTime);

            run.AddItems(await serviceProvider.GetRequiredService<ILateFeeService>().AssessDueAsync(today, cancellationToken), "debit_notes");
        });
}

/// <summary>
/// Adds Commerce > Late fees.
/// </summary>
public sealed class LateFeePoliciesAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", InvoicingConstants.Features.Area },
        { "controller", "LateFeePolicies" },
        { "action", "Index" },
    };

    internal readonly IStringLocalizer S;

    public LateFeePoliciesAdminMenu(IStringLocalizer<LateFeePoliciesAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Late fees"], S["Late fees"].PrefixPosition("4.4"), fees => fees
                    .Id("latefees")
                    .Action("Index", "LateFeePolicies", _routeValues)
                    .Permission(InvoicingPermissions.ManageInvoicingSettings)
                    .LocalNav())
                .Add(S["Statements"], S["Statements"].PrefixPosition("4.5"), statements => statements
                    .Id("invoicestatements")
                    .Action("Index", "StatementsAdmin", new RouteValueDictionary { { "area", InvoicingConstants.Features.Area } })
                    .Permission(InvoicingPermissions.ViewInvoices)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Lists, creates, changes and deletes the late-fee policies.
/// </summary>
[Admin]
[Feature(InvoicingConstants.Features.Collections)]
public sealed class LateFeePoliciesController : CatalogAdminController<LateFeePolicy>
{
    public LateFeePoliciesController(
        ILateFeePolicyManager manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<LateFeePolicy> displayManager,
        INotifier notifier,
        IHtmlLocalizer<LateFeePoliciesController> htmlLocalizer,
        IStringLocalizer<LateFeePoliciesController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    /// <inheritdoc/>
    protected override Permission ManagePermission
        => InvoicingPermissions.ManageInvoicingSettings;

    /// <inheritdoc/>
    protected override LocalizedString CreateDisplayName
        => S["Late-fee policy"];

    /// <inheritdoc/>
    protected override LocalizedString NewDisplayName
        => S["New late-fee policy"];

    /// <inheritdoc/>
    protected override LocalizedHtmlString CreatedNotification
        => H["The late-fee policy has been added."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString UpdatedNotification
        => H["The late-fee policy has been updated. Late fees already charged stay as they are."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString DeletedNotification
        => H["The late-fee policy has been deleted. Late fees already charged stay as they are."];

    /// <summary>
    /// Lists the policies.
    /// </summary>
    [Admin("commerce/late-fees", "LateFeePoliciesIndex")]
    public Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
        => IndexAsync(options, pagerParameters, pagerOptions, shapeFactory);

    /// <summary>
    /// Applies the search.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("commerce/late-fees", "LateFeePoliciesIndex")]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
        => IndexFilterPostAsync(model, pagerParameters);

    /// <summary>
    /// Shows the editor of a new policy.
    /// </summary>
    [Admin("commerce/late-fees/create", "LateFeePoliciesCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    /// <summary>
    /// Saves a new policy.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("commerce/late-fees/create", "LateFeePoliciesCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    /// <summary>
    /// Shows the editor of a policy.
    /// </summary>
    [Admin("commerce/late-fees/edit/{id}", "LateFeePoliciesEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    /// <summary>
    /// Saves a policy.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("commerce/late-fees/edit/{id}", "LateFeePoliciesEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    /// <summary>
    /// Deletes a policy.
    /// </summary>
    [HttpPost]
    [Admin("commerce/late-fees/delete/{id}", "LateFeePoliciesDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}

/// <summary>
/// Builds the row of a late-fee policy in the admin list, in both layouts, and its editor.
/// </summary>
public sealed class LateFeePolicyDisplayDriver : DisplayDriver<LateFeePolicy>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(LateFeePolicy policy, BuildDisplayContext context)
    {
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = policy.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("LateFeePolicy_Fields_SummaryAdmin", policy)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("LateFeePolicy_Tags_SummaryAdmin", policy)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Fee:5"),
            View("LateFeePolicy_Buttons_SummaryAdmin", policy).Location("Actions:5"));
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(LateFeePolicy policy, BuildEditorContext context)
        => Initialize<LateFeePolicyViewModel>("LateFeePolicyFields_Edit", model =>
        {
            model.Name = policy.Name;
            model.Description = policy.Description;
            model.Enabled = policy.Enabled;
            model.Kind = policy.Kind;
            model.Amount = policy.Kind == LateFeeKind.Flat ? policy.Amount : null;
            model.Percent = policy.Kind == LateFeeKind.Percent ? policy.Percent : null;
            model.GraceDays = policy.GraceDays;
            model.RepeatEveryDays = policy.RepeatEveryDays > 0 ? policy.RepeatEveryDays : null;
            model.MaxFees = policy.MaxFees;
            model.CapAmount = policy.CapAmount;
            model.MinimumBalance = policy.MinimumBalance > 0m ? policy.MinimumBalance : null;
            model.Taxable = policy.Taxable;
            model.SendDebitNote = policy.SendDebitNote;
        }).Location("Content:1");

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(LateFeePolicy policy, UpdateEditorContext context)
    {
        var model = new LateFeePolicyViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        policy.Name = model.Name?.Trim();
        policy.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        policy.Enabled = model.Enabled;
        policy.Kind = model.Kind;
        policy.Amount = model.Kind == LateFeeKind.Flat ? model.Amount ?? 0m : 0m;
        policy.Percent = model.Kind == LateFeeKind.Percent ? model.Percent ?? 0m : 0m;
        policy.GraceDays = Math.Max(0, model.GraceDays);
        policy.RepeatEveryDays = model.RepeatEveryDays is > 0 ? model.RepeatEveryDays.Value : 0;
        policy.MaxFees = policy.RepeatEveryDays > 0 && model.MaxFees is > 0 ? model.MaxFees : null;
        policy.CapAmount = model.CapAmount is > 0m ? model.CapAmount : null;
        policy.MinimumBalance = Math.Max(0m, model.MinimumBalance ?? 0m);
        policy.Taxable = model.Taxable;
        policy.SendDebitNote = model.SendDebitNote;

        return Edit(policy, context);
    }
}

/// <summary>
/// Shows, on an invoice's page, the late fees charged on it, and on a debit note the invoice it charges a fee on.
/// </summary>
public sealed class InvoiceLateFeesDisplayDriver : DisplayDriver<Invoice>
{
    private readonly ILateFeeService _lateFees;
    private readonly ILateFeePolicyManager _policies;
    private readonly IInvoiceManager _invoices;
    private readonly ILocalClock _localClock;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public InvoiceLateFeesDisplayDriver(
        ILateFeeService lateFees,
        ILateFeePolicyManager policies,
        IInvoiceManager invoices,
        ILocalClock localClock,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _lateFees = lateFees;
        _policies = policies;
        _invoices = invoices;
        _localClock = localClock;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    public override IDisplayResult Display(Invoice invoice, BuildDisplayContext context)
    {
        if (!string.Equals(context.DisplayType, InvoicingConstants.DisplayTypes.DetailAdmin, StringComparison.Ordinal) || invoice.Status == InvoiceStatus.Draft)
        {
            return null;
        }

        return Initialize<InvoiceLateFeesViewModel>("InvoiceLateFees_DetailAdmin", async model =>
        {
            model.Invoice = invoice;

            if (invoice.Kind == InvoiceKind.DebitNote)
            {
                model.RelatedInvoice = string.IsNullOrEmpty(invoice.RelatedInvoiceId) ? null : await _invoices.FindByIdAsync(invoice.RelatedInvoiceId);
            }
            else
            {
                model.DebitNotes = await _lateFees.ListDebitNotesAsync(invoice.ItemId);

                // An overdue invoice can be charged the fee due now, rather than at the next run of the task.
                var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

                model.CanAssess = invoice is { Status: InvoiceStatus.Open, LateFeesExempt: false, DueDate: { } due } &&
                    due < today &&
                    invoice.AmountDue > 0m &&
                    (await _policies.GetEnabledAsync()).Count > 0 &&
                    await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, InvoicingPermissions.IssueInvoices);
            }
        }).Location(InvoicingConstants.DisplayTypes.DetailAdmin, "Content:9");
    }
}

/// <summary>
/// Charges the late fee due on an overdue invoice now, from its page.
/// </summary>
[Admin("invoices/late-fees/{action}/{itemId}", "InvoiceLateFees{action}")]
[Feature(InvoicingConstants.Features.Collections)]
public sealed class InvoiceLateFeesController : Controller
{
    private readonly ILateFeeService _lateFees;
    private readonly ILocalClock _localClock;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public InvoiceLateFeesController(
        ILateFeeService lateFees,
        ILocalClock localClock,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<InvoiceLateFeesController> htmlLocalizer)
    {
        _lateFees = lateFees;
        _localClock = localClock;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Charges the next late fee due on the invoice under each policy.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Assess(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.IssueInvoices))
        {
            return Forbid();
        }

        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);
        var result = await _lateFees.AssessAsync(itemId, today, HttpContext.RequestAborted);

        foreach (var error in result.Errors)
        {
            await _notifier.WarningAsync(new LocalizedHtmlString(error, "{0}", false, error));
        }

        if (result.DebitNotes.Count > 0)
        {
            await _notifier.SuccessAsync(H["Late fee {0} was charged.", string.Join(", ", result.DebitNotes.Select(note => note.Number))]);
        }
        else if (result.Errors.Count == 0)
        {
            await _notifier.InformationAsync(H["No late fee is due on this invoice now."]);
        }

        return RedirectToAction("Detail", "InvoicesAdmin", new { itemId });
    }
}

/// <summary>
/// Exports every late-fee policy.
/// </summary>
public sealed class LateFeePoliciesDeploymentStep : DeploymentStep
{
    public const string StepName = "LateFeePolicies";

    public const string EntriesName = "Policies";

    private static readonly LocalizationSource _category = LocalizationSource.Create<LateFeePoliciesDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<LateFeePoliciesDeploymentStep>("Late-fee policies");

    public override string Name => StepName;

    [JsonIgnore]
    public override LocalizationSource Category => _category;

    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class LateFeePoliciesDeploymentSource : CatalogDeploymentSource<LateFeePoliciesDeploymentStep, LateFeePolicy>
{
    public LateFeePoliciesDeploymentSource(ILateFeePolicyManager manager)
        : base(LateFeePoliciesDeploymentStep.EntriesName, manager)
    {
    }
}

/// <summary>
/// Creates or updates the late-fee policies a recipe lists under <c>Policies</c>, matched by id.
/// </summary>
internal sealed class LateFeePoliciesStep : CatalogRecipeStep<LateFeePolicy>
{
    public LateFeePoliciesStep(ILateFeePolicyManager manager)
        : base(LateFeePoliciesDeploymentStep.StepName, LateFeePoliciesDeploymentStep.EntriesName, manager)
    {
    }
}

internal sealed class LateFeePoliciesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(LateFeePoliciesDeploymentStep);

    protected override string DisplayText => "Late-fee policies";

    protected override string Description => "Exports every late-fee policy.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}

internal sealed class LateFeePoliciesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => LateFeePoliciesDeploymentStep.StepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(LateFeePoliciesDeploymentStep.StepName).Description("Recipe step discriminator. Must be 'LateFeePolicies'.")),
                (LateFeePoliciesDeploymentStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The policy's 26-character id. When it matches a stored policy, that policy is updated.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The policy's name.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the policy charges fees.")),
                            ("Kind", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Integer).Description("Flat or Percent.")),
                            ("Amount", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The fixed amount of each fee, in the invoice's currency.")),
                            ("Percent", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The percent of what is still due each fee charges.")),
                            ("GraceDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("How many days after the due date pass before the first fee.")),
                            ("RepeatEveryDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("How many days after a fee the next one is charged; 0 charges one fee.")),
                            ("MaxFees", new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Description("The most fees charged on one invoice.")),
                            ("CapAmount", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("The most charged in fees on one invoice.")),
                            ("MinimumBalance", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The least that must still be due for a fee.")),
                            ("Taxable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the fee is taxed like a sale.")),
                            ("SendDebitNote", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether each debit note is e-mailed once issued.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The late-fee policies to create or update.")))
            .Required("name", LateFeePoliciesDeploymentStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}

[Feature(InvoicingConstants.Features.Collections)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class CollectionsDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDeployment<LateFeePoliciesDeploymentSource, LateFeePoliciesDeploymentStep, SummaryDeploymentStepDisplayDriver<LateFeePoliciesDeploymentStep>>();
}

[Feature(InvoicingConstants.Features.Collections)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class CollectionsRecipeSchemasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, LateFeePoliciesRecipeStepSchema>();
        services.AddDeploymentStepSchema<LateFeePoliciesDeploymentStepSchema>();
    }
}
