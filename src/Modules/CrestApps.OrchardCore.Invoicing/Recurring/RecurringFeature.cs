using CrestApps.Core.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Globalization;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Calendars.Recurrence;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Migrations;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Reports;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using CrestApps.OrchardCore.Invoicing.Workflows;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Webhooks;
using Json.Schema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DataSources;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Workflows.Helpers;
using OrchardCore.Workflows.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.Recurring;

/// <summary>
/// Registers recurring plans: the services, the index and its migration, the admin pages, the run task and the recipe
/// step.
/// </summary>
[Feature(InvoicingConstants.Features.Recurring)]
public sealed class RecurringStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddInvoicingRecurring()
            .AddIndexProvider<RecurringPlanIndexProvider>()
            .AddDataMigration<RecurringPlansMigrations>()
            .AddRecipeExecutionStep<RecurringPlansStep>()
            .AddDisplayDriver<RecurringPlan, RecurringPlanDisplayDriver>()
            .AddNavigationProvider<RecurringPlansAdminMenu>();

        // With the recipe step, so an imported plan gets the editor's rules.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<RecurringPlan>, RecurringPlanHandler>());

        services.AddSingleton<IBackgroundTask, RecurringInvoiceBackgroundTask>();
    }
}

/// <summary>
/// Makes the invoices of the recurring plans' runs that are due, a day in the site's time zone.
/// </summary>
[BackgroundTask(
    Title = "Recurring Invoices",
    Schedule = "*/30 * * * *",
    Description = "Makes the invoices of recurring plans whose next run is due.",
    LockTimeout = 5_000,
    LockExpiration = 300_000)]
public sealed class RecurringInvoiceBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run =>
        {
            var localClock = serviceProvider.GetRequiredService<ILocalClock>();
            var today = DateOnly.FromDateTime((await localClock.GetLocalNowAsync()).DateTime);

            run.AddItems(await serviceProvider.GetRequiredService<IRecurringPlanService>().RunDueAsync(today, cancellationToken), "generated");
        });
}

/// <summary>
/// Adds Commerce > Recurring invoices, beside Invoices.
/// </summary>
public sealed class RecurringPlansAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", InvoicingConstants.Features.Area },
        { "controller", "RecurringPlansAdmin" },
        { "action", "Index" },
    };

    internal readonly IStringLocalizer S;

    public RecurringPlansAdminMenu(IStringLocalizer<RecurringPlansAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Recurring invoices"], S["Recurring invoices"].PrefixPosition("4.2"), plans => plans
                    .Id("recurringinvoices")
                    .Action("Index", "RecurringPlansAdmin", _routeValues)
                    .Permission(InvoicingPermissions.ViewInvoices)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// The row of a recurring plan in its admin list, in the List and Grid layouts.
/// </summary>
public sealed class RecurringPlanDisplayDriver : DisplayDriver<RecurringPlan>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public RecurringPlanDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> DisplayAsync(RecurringPlan plan, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        var model = new RecurringPlanRowViewModel
        {
            Plan = plan,
            CanEdit = plan.Status is not (RecurringPlanStatus.Completed or RecurringPlanStatus.Canceled) &&
                await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, InvoicingPermissions.ManageRecurringInvoices),
        };

        return Combine(
            View("RecurringPlan_Fields_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("RecurringPlan_Status_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("RecurringPlan_Meta_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Schedule:5"),
            View("RecurringPlan_Buttons_SummaryAdmin", model).Location("Actions:5"));
    }
}

/// <summary>
/// Turns the editor's simple schedule into a recurrence rule and back.
/// </summary>
internal static class RecurringSchedule
{
    public static string BuildRule(string frequency, int interval, string customRule)
    {
        if (!string.IsNullOrWhiteSpace(customRule))
        {
            return customRule.Trim().ToUpperInvariant().Replace("RRULE:", string.Empty, StringComparison.Ordinal);
        }

        var freq = frequency switch
        {
            "Weekly" => "WEEKLY",
            "Yearly" => "YEARLY",
            _ => "MONTHLY",
        };

        return interval > 1 ? $"FREQ={freq};INTERVAL={interval.ToString(CultureInfo.InvariantCulture)}" : $"FREQ={freq}";
    }

    public static (string Frequency, int Interval, string CustomRule) Read(string rule)
    {
        if (string.IsNullOrWhiteSpace(rule) || !RecurrenceRule.TryParse(rule, out var parsed, out _))
        {
            return ("Monthly", 1, null);
        }

        var simple = parsed.Count is null && parsed.Until is null && parsed.ByDay.Count == 0 && parsed.ByMonthDay.Count == 0 && parsed.ByMonth.Count == 0 && parsed.BySetPosition.Count == 0;

        return parsed.Frequency switch
        {
            RecurrenceFrequency.Weekly when simple => ("Weekly", parsed.Interval, null),
            RecurrenceFrequency.Monthly when simple => ("Monthly", parsed.Interval, null),
            RecurrenceFrequency.Yearly when simple => ("Yearly", parsed.Interval, null),
            _ => ("Monthly", 1, rule),
        };
    }
}

/// <summary>
/// The input a recurring plan workflow event starts with.
/// </summary>
internal static class RecurringWorkflowInput
{
    public static Dictionary<string, object> ToDictionary(RecurringPlan plan, RecurringPlanRun run, Invoice invoice)
    {
        var input = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["PlanId"] = plan.ItemId,
            ["PlanName"] = plan.Name,
            ["Occurrence"] = run.Occurrence.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["Outcome"] = run.Outcome.ToString(),
            ["Message"] = run.Message,
            ["RunCount"] = plan.RunCount,
            ["NextRunDate"] = plan.NextRunDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };

        if (invoice is not null)
        {
            foreach (var (key, value) in InvoiceWorkflowInput.ToDictionary(invoice))
            {
                input[key] = value;
            }
        }
        else
        {
            input["InvoiceId"] = run.InvoiceId;
        }

        return input;
    }
}

/// <summary>
/// Starts when a recurring plan makes an invoice; the input holds the plan, the occurrence and the invoice.
/// </summary>
public sealed class RecurringInvoiceGeneratedEvent : InvoiceEventActivity
{
    public RecurringInvoiceGeneratedEvent(IStringLocalizer<RecurringInvoiceGeneratedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Name => nameof(RecurringInvoiceGeneratedEvent);

    public override LocalizedString DisplayText => S["Recurring Invoice Generated"];
}

/// <summary>
/// Starts when a run of a recurring plan fails; the input's <c>Message</c> says why.
/// </summary>
public sealed class RecurringRunFailedEvent : InvoiceEventActivity
{
    public RecurringRunFailedEvent(IStringLocalizer<RecurringRunFailedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Name => nameof(RecurringRunFailedEvent);

    public override LocalizedString DisplayText => S["Recurring Run Failed"];
}

internal sealed class RecurringWorkflowObserver : IRecurringPlanObserver
{
    private readonly IWorkflowManager _workflowManager;

    public RecurringWorkflowObserver(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    public Task GeneratedAsync(RecurringPlan plan, Invoice invoice, RecurringPlanRun run, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(RecurringInvoiceGeneratedEvent), plan, RecurringWorkflowInput.ToDictionary(plan, run, invoice));

    public Task RunFailedAsync(RecurringPlan plan, RecurringPlanRun run, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(RecurringRunFailedEvent), plan, RecurringWorkflowInput.ToDictionary(plan, run, null));

    private Task TriggerAsync(string eventName, RecurringPlan plan, Dictionary<string, object> input)
    {
        if (ShellScope.Current is null)
        {
            return _workflowManager.TriggerEventAsync(eventName, input, plan.ItemId);
        }

        DeferredWork.Add(InvoiceWorkflowObserver.WorkName, scope => scope.ServiceProvider
            .GetRequiredService<IWorkflowManager>()
            .TriggerEventAsync(eventName, input, plan.ItemId));

        return Task.CompletedTask;
    }
}

internal sealed class RecurringWebhookObserver : IRecurringPlanObserver
{
    private readonly IWebhookPublisher _publisher;

    public RecurringWebhookObserver(IWebhookPublisher publisher)
    {
        _publisher = publisher;
    }

    public Task GeneratedAsync(RecurringPlan plan, Invoice invoice, RecurringPlanRun run, CancellationToken cancellationToken = default)
        => PublishAsync(InvoicingConstants.WebhookEvents.RecurringInvoiceGenerated, plan, run, invoice, cancellationToken);

    public Task RunFailedAsync(RecurringPlan plan, RecurringPlanRun run, CancellationToken cancellationToken = default)
        => PublishAsync(InvoicingConstants.WebhookEvents.RecurringRunFailed, plan, run, null, cancellationToken);

    private Task PublishAsync(string eventName, RecurringPlan plan, RecurringPlanRun run, Invoice invoice, CancellationToken cancellationToken)
    {
        var occurrence = run.Occurrence.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return _publisher.PublishAsync(eventName, new
        {
            plan = new { id = plan.ItemId, name = plan.Name, status = plan.Status.ToString(), runCount = plan.RunCount, nextRunDate = plan.NextRunDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
            run = new { occurrence, outcome = run.Outcome.ToString(), message = run.Message, invoiceId = run.InvoiceId, invoiceNumber = run.InvoiceNumber },
            invoice = invoice is null ? null : new { id = invoice.ItemId, number = invoice.Number, status = invoice.Status.ToString(), currency = invoice.Currency, total = invoice.Totals?.Total ?? 0m },
        }, $"evt_{eventName.Replace('.', '-')}_{plan.ItemId}_{occurrence}", cancellationToken);
    }
}

[Feature(InvoicingConstants.Features.Recurring)]
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class RecurringWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecurringPlanObserver, RecurringWorkflowObserver>();
        services.AddActivity<RecurringInvoiceGeneratedEvent, InvoiceEventDisplayDriver<RecurringInvoiceGeneratedEvent>>();
        services.AddActivity<RecurringRunFailedEvent, InvoiceEventDisplayDriver<RecurringRunFailedEvent>>();
    }
}

[Feature(InvoicingConstants.Features.Recurring)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class RecurringWorkflowsRecipesSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services
            .AddWorkflowActivitySchema<RecurringInvoiceGeneratedEventSchema>()
            .AddWorkflowActivitySchema<RecurringRunFailedEventSchema>();
}

[Feature(InvoicingConstants.Features.Recurring)]
[RequireFeatures(CommerceConstants.Features.Webhooks)]
public sealed class RecurringWebhooksStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecurringPlanObserver, RecurringWebhookObserver>();
        services.AddWebhookEvent(InvoicingConstants.WebhookEvents.RecurringInvoiceGenerated, "Recurring invoice generated", "Invoicing");
        services.AddWebhookEvent(InvoicingConstants.WebhookEvents.RecurringRunFailed, "Recurring run failed", "Invoicing");
    }
}

internal sealed class RecurringInvoiceGeneratedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => nameof(RecurringInvoiceGeneratedEvent);

    protected override string Category => "Invoicing";

    protected override string DisplayText => "Recurring Invoice Generated";

    protected override string Description => "Starts when a recurring plan makes an invoice";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}

internal sealed class RecurringRunFailedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => nameof(RecurringRunFailedEvent);

    protected override string Category => "Invoicing";

    protected override string DisplayText => "Recurring Run Failed";

    protected override string Description => "Starts when a run of a recurring plan fails";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}

/// <summary>
/// Exports every recurring plan.
/// </summary>
public sealed class RecurringPlansDeploymentStep : DeploymentStep
{
    public const string StepName = "RecurringPlans";

    public const string EntriesName = "Plans";

    private static readonly LocalizationSource _category = LocalizationSource.Create<RecurringPlansDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<RecurringPlansDeploymentStep>("Recurring invoices");

    public override string Name => StepName;

    [JsonIgnore]
    public override LocalizationSource Category => _category;

    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class RecurringPlansDeploymentSource : CatalogDeploymentSource<RecurringPlansDeploymentStep, RecurringPlan>
{
    public RecurringPlansDeploymentSource(IRecurringPlanManager manager)
        : base(RecurringPlansDeploymentStep.EntriesName, manager)
    {
    }
}

/// <summary>
/// Creates or updates the recurring plans a recipe lists under <c>Plans</c>, matched by id.
/// </summary>
internal sealed class RecurringPlansStep : CatalogRecipeStep<RecurringPlan>
{
    public RecurringPlansStep(IRecurringPlanManager manager)
        : base(RecurringPlansDeploymentStep.StepName, RecurringPlansDeploymentStep.EntriesName, manager)
    {
    }
}

internal sealed class RecurringPlansDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(RecurringPlansDeploymentStep);

    protected override string DisplayText => "Recurring invoices";

    protected override string Description => "Exports every recurring plan with its schedule, lines and runs.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}

internal sealed class RecurringPlansRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => RecurringPlansDeploymentStep.StepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(RecurringPlansDeploymentStep.StepName).Description("Recipe step discriminator. Must be 'RecurringPlans'.")),
                (RecurringPlansDeploymentStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The plan's 26-character id. When it matches a stored plan, that plan is updated.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The plan's name.")),
                            ("Rule", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The recurrence rule (RFC 5545), such as FREQ=MONTHLY;BYMONTHDAY=1.")),
                            ("StartDate", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The day of the first run, yyyy-MM-dd.")),
                            ("EndDate", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The last day a run can happen, yyyy-MM-dd.")),
                            ("MaxRuns", new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Description("How many runs the plan makes in all.")),
                            ("Mode", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Integer).Description("Draft, Issue, IssueAndSend or IssueAndCharge.")),
                            ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Integer).Description("Draft, Active, Paused, Completed or Canceled.")),
                            ("Lines", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The lines of each invoice, as on an invoice; {{ period.start }}, {{ period.end }}, {{ period.month }} and {{ period.year }} are written in.")))
                        .Required("Name", "Rule")
                        .AdditionalProperties(true))
                    .Description("The recurring plans to create or update.")))
            .Required("name", RecurringPlansDeploymentStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}

[Feature(InvoicingConstants.Features.Recurring)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class RecurringDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDeployment<RecurringPlansDeploymentSource, RecurringPlansDeploymentStep>();
}

[Feature(InvoicingConstants.Features.Recurring)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecurringRecipeSchemasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, RecurringPlansRecipeStepSchema>();
        services.AddDeploymentStepSchema<RecurringPlansDeploymentStepSchema>();
    }
}

/// <summary>
/// One row per recurring plan: its name, schedule, status, next run and totals.
/// </summary>
public sealed class RecurringPlansReportDataSet : IndexedReportDataSet<RecurringPlan, RecurringPlanIndex>, IInvoicingReportDataSet
{
    public RecurringPlansReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<RecurringPlansReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.InvoicingSource.RecurringPlans,
                stringLocalizer["Recurring plans"],
                stringLocalizer["One row per recurring plan: name, schedule, status, next run, runs made and the total of each invoice."]),
            session,
            authorizationService,
            InvoicingConstants.CollectionName,
            InvoicingPermissions.ViewInvoices)
    {
        var S = stringLocalizer;
        var plan = S["Plan"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Plan ID"], DataFieldType.Text, record => record.ItemId, plan).Identifier();
        AddField(nameof(RecurringPlan.Name), S["Name"], DataFieldType.Text, record => record.Name, plan);
        AddField(nameof(RecurringPlan.Status), S["Status"], DataFieldType.Text, record => record.Status, plan);
        AddField(nameof(RecurringPlan.Mode), S["Each run"], DataFieldType.Text, record => record.Mode, plan);
        AddField(nameof(RecurringPlan.Rule), S["Schedule"], DataFieldType.Text, record => record.Rule, plan);
        AddField("BillToName", S["Customer"], DataFieldType.Text, record => record.BillTo?.Contact?.Name, plan);
        AddField(nameof(RecurringPlan.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, plan);
        AddField("Total", S["Total of each invoice"], DataFieldType.Decimal, record => record.Totals?.Total ?? 0m, plan);
        AddField(nameof(RecurringPlan.RunCount), S["Invoices made"], DataFieldType.Integer, record => record.RunCount, plan);
        AddField("StartDate", S["Starts"], DataFieldType.Date, record => record.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), plan);
        AddField("EndDate", S["Ends"], DataFieldType.Date, record => record.EndDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), plan);
        AddField("NextRunDate", S["Next run"], DataFieldType.Date, record => record.NextRunDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), plan);
        AddField("CreatedUtc", S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, plan);

        CompleteDescription();
    }

    protected override (string Field, System.Linq.Expressions.Expression<Func<RecurringPlanIndex, DateTime>> Column)? DateColumn
        => ("CreatedUtc", index => index.CreatedUtc);
}

[Feature(InvoicingConstants.Features.Recurring)]
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class RecurringDataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<IInvoicingReportDataSet, RecurringPlansReportDataSet>();
}
