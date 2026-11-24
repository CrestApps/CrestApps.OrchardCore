using CrestApps.Core.Services;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Endpoints;
using CrestApps.OrchardCore.Invoicing.Migrations;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
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

namespace CrestApps.OrchardCore.Invoicing.Billing;

/// <summary>
/// Registers the usage meters: their admin pages and indexes, the page that records and lists a meter's usage, and their
/// recipe and deployment steps. Recurring plans bill a meter's usage through <see cref="RecurringPlan.UsageCharges"/>.
/// </summary>
[Feature(InvoicingConstants.Features.Billing)]
public sealed class UsageStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddIndexProvider<UsageMeterIndexProvider>()
            .AddIndexProvider<UsageRecordIndexProvider>()
            .AddDataMigration<UsageMigrations>()
            .AddRecipeExecutionStep<UsageMetersStep>()
            .AddDisplayDriver<UsageMeter, UsageMeterDisplayDriver>()
            .AddNavigationProvider<UsageMetersAdminMenu>();

        // With the recipe step, so an imported meter gets the editor's rules.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<UsageMeter>, UsageMeterHandler>());
    }
}

/// <summary>
/// Adds Commerce > Usage Meters.
/// </summary>
public sealed class UsageMetersAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public UsageMetersAdminMenu(IStringLocalizer<UsageMetersAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Usage Meters"], S["Usage Meters"].PrefixPosition("4.7"), meters => meters
                    .Id("usagemeters")
                    .Action("Index", "UsageMeters", new RouteValueDictionary { { "area", InvoicingConstants.Features.Area } })
                    .Permission(InvoicingPermissions.ManageInvoices)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Lists, adds, changes and deletes the usage meters, and records and lists a meter's usage.
/// </summary>
[Admin]
[Feature(InvoicingConstants.Features.Billing)]
public sealed class UsageMetersController : CatalogAdminController<UsageMeter>
{
    private const int RecentRecords = 50;

    private readonly IUsageMeterManager _meters;

    public UsageMetersController(
        IUsageMeterManager manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<UsageMeter> displayManager,
        INotifier notifier,
        IHtmlLocalizer<UsageMetersController> htmlLocalizer,
        IStringLocalizer<UsageMetersController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
        _meters = manager;
    }

    protected override Permission ManagePermission => InvoicingPermissions.ManageInvoices;

    protected override LocalizedString CreateDisplayName => S["Usage meter"];

    protected override LocalizedString NewDisplayName => S["New usage meter"];

    protected override LocalizedHtmlString CreatedNotification => H["The meter was added. Record usage on it, and bill it from a recurring plan."];

    protected override LocalizedHtmlString UpdatedNotification => H["The meter was updated."];

    protected override LocalizedHtmlString DeletedNotification => H["The meter was deleted. Its usage records stay, and plans that billed it stop at their next run until another meter is chosen."];

    [Admin("commerce/usage-meters", "UsageMetersIndex")]
    public Task<IActionResult> Index(CatalogEntryOptions options, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
        => IndexAsync(options, pagerParameters, pagerOptions, shapeFactory);

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("commerce/usage-meters", "UsageMetersIndex")]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
        => IndexFilterPostAsync(model, pagerParameters);

    [Admin("commerce/usage-meters/create", "UsageMetersCreate")]
    public Task<IActionResult> Create() => CreateAsync();

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("commerce/usage-meters/create", "UsageMetersCreate")]
    public Task<IActionResult> CreatePost() => CreatePostAsync();

    [Admin("commerce/usage-meters/edit/{id}", "UsageMetersEdit")]
    public Task<IActionResult> Edit(string id) => EditAsync(id);

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("commerce/usage-meters/edit/{id}", "UsageMetersEdit")]
    public Task<IActionResult> EditPost(string id) => EditPostAsync(id);

    [HttpPost]
    [Admin("commerce/usage-meters/delete/{id}", "UsageMetersDelete")]
    public Task<IActionResult> Delete(string id) => DeleteAsync(id);

    /// <summary>
    /// Shows a meter's latest usage and a form to record usage by hand.
    /// </summary>
    [HttpGet]
    [Admin("commerce/usage-meters/usage/{id}", "UsageMetersUsage")]
    public async Task<IActionResult> Usage(string id, [FromServices] IUsageService usage, [FromServices] IPartyDirectory parties, [FromServices] ILocalClock localClock)
    {
        if (!await AuthorizationService.AuthorizeAsync(User, ManagePermission))
        {
            return Forbid();
        }

        var meter = await _meters.FindByIdAsync(id);

        if (meter is null)
        {
            return NotFound();
        }

        var model = new UsageMeterUsageViewModel
        {
            Meter = meter,
            OccurredLocal = (await localClock.GetLocalNowAsync()).DateTime,
            PartySearchEndpoint = Url.RouteUrl(InvoicePartySearchEndpoints.RouteName),
        };
        var names = new Dictionary<PartyRef, string>();

        foreach (var record in await usage.ListRecentAsync(meter.ItemId, RecentRecords, HttpContext.RequestAborted))
        {
            if (!names.TryGetValue(record.Party, out var name))
            {
                name = record.Party.IsEmpty ? null : await parties.GetNameAsync(record.Party) ?? record.Party.ToString();
                names[record.Party] = name;
            }

            model.Records.Add(new UsageRecordRowViewModel
            {
                Record = record,
                PartyName = name,
                OccurredLocal = (await localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(record.OccurredUtc, DateTimeKind.Utc)))).DateTime,
            });
        }

        return View(model);
    }

    /// <summary>
    /// Records usage by hand: a quantity for a customer at a time, once per event id.
    /// </summary>
    [HttpPost]
    [Admin("commerce/usage-meters/record/{id}", "UsageMetersRecord")]
    public async Task<IActionResult> Record(string id, string party, decimal quantity, DateTime? occurredLocal, string eventId, [FromServices] IUsageService usage, [FromServices] ILocalClock localClock)
    {
        if (!await AuthorizationService.AuthorizeAsync(User, ManagePermission))
        {
            return Forbid();
        }

        var result = await usage.RecordAsync(new UsageRecordRequest
        {
            Meter = id,
            Party = PartyRef.TryParse(party?.Trim(), out var partyRef) ? partyRef : default,
            Quantity = quantity,
            OccurredUtc = occurredLocal is { } local ? await localClock.ToUtcLenientAsync(local) : null,
            EventId = eventId,
        }, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                await Notifier.ErrorAsync(new LocalizedHtmlString(error, HtmlEncoder.Default.Encode(error)));
            }
        }
        else if (result.AlreadyRecorded)
        {
            await Notifier.InformationAsync(H["That event was recorded before, so nothing new was recorded."]);
        }
        else
        {
            await Notifier.SuccessAsync(H["The usage was recorded."]);
        }

        return RedirectToAction(nameof(Usage), new { id });
    }
}

/// <summary>
/// The row of a meter in its list, in both layouts, and its editor.
/// </summary>
public sealed class UsageMeterDisplayDriver : DisplayDriver<UsageMeter>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(UsageMeter meter, BuildDisplayContext context)
    {
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{meter.Name} {meter.Code}".ToLowerInvariant();
        }

        return CombineAsync(
            View("UsageMeter_Fields_SummaryAdmin", meter)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("UsageMeter_Tags_SummaryAdmin", meter)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Meter:5"),
            View("UsageMeter_Buttons_SummaryAdmin", meter).Location("Actions:5"));
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(UsageMeter meter, BuildEditorContext context)
        => Initialize<UsageMeterViewModel>("UsageMeterFields_Edit", model =>
        {
            model.Name = meter.Name;
            model.Code = meter.Code;
            model.Unit = meter.Unit;
            model.Aggregation = meter.Aggregation;
            model.Enabled = meter.Enabled;
        }).Location("Content:1");

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(UsageMeter meter, UpdateEditorContext context)
    {
        var model = new UsageMeterViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        meter.Name = model.Name?.Trim();
        meter.Code = model.Code?.Trim().ToLowerInvariant();
        meter.Unit = string.IsNullOrWhiteSpace(model.Unit) ? null : model.Unit.Trim();
        meter.Aggregation = model.Aggregation;
        meter.Enabled = model.Enabled;

        return Edit(meter, context);
    }
}

/// <summary>
/// Exports every usage meter (not their usage).
/// </summary>
public sealed class UsageMetersDeploymentStep : DeploymentStep
{
    public const string StepName = "UsageMeters";

    public const string EntriesName = "Meters";

    private static readonly LocalizationSource _category = LocalizationSource.Create<UsageMetersDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<UsageMetersDeploymentStep>("Usage meters");

    public override string Name => StepName;

    [JsonIgnore]
    public override LocalizationSource Category => _category;

    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class UsageMetersDeploymentSource : CatalogDeploymentSource<UsageMetersDeploymentStep, UsageMeter>
{
    public UsageMetersDeploymentSource(IUsageMeterManager manager)
        : base(UsageMetersDeploymentStep.EntriesName, manager)
    {
    }
}

/// <summary>
/// Creates or updates the usage meters a recipe lists under <c>Meters</c>, matched by id.
/// </summary>
internal sealed class UsageMetersStep : CatalogRecipeStep<UsageMeter>
{
    public UsageMetersStep(IUsageMeterManager manager)
        : base(UsageMetersDeploymentStep.StepName, UsageMetersDeploymentStep.EntriesName, manager)
    {
    }
}

internal sealed class UsageMetersDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(UsageMetersDeploymentStep);

    protected override string DisplayText => "Usage meters";

    protected override string Description => "Exports every usage meter, without its usage.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}

internal sealed class UsageMetersRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => UsageMetersDeploymentStep.StepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(UsageMetersDeploymentStep.StepName).Description("Recipe step discriminator. Must be 'UsageMeters'.")),
                (UsageMetersDeploymentStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The meter's 26-character id. When it matches a stored meter, that meter is updated.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The meter's name.")),
                            ("Code", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The key usage is recorded with, such as api-calls: lowercase letters, digits, dots, dashes or underscores.")),
                            ("Unit", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The unit printed on invoices, such as calls or GB.")),
                            ("Aggregation", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Integer).Description("How a period's usage adds up: Sum, Maximum or Last.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the meter takes usage.")))
                        .Required("Name", "Code")
                        .AdditionalProperties(true))
                    .Description("The usage meters to create or update.")))
            .Required("name", UsageMetersDeploymentStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}

[Feature(InvoicingConstants.Features.Billing)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class UsageDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDeployment<UsageMetersDeploymentSource, UsageMetersDeploymentStep, SummaryDeploymentStepDisplayDriver<UsageMetersDeploymentStep>>();
}

[Feature(InvoicingConstants.Features.Billing)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class UsageRecipeSchemasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, UsageMetersRecipeStepSchema>();
        services.AddDeploymentStepSchema<UsageMetersDeploymentStepSchema>();
    }
}
