using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Deployments;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Reports;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.Workflows;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Webhooks;
using Json.Schema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Deployment;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Recipes;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Helpers;
using OrchardCore.Workflows.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.Estimates;

/// <summary>
/// The input an estimate workflow event starts with: <c>Workflow.Input.EstimateId</c>, <c>Number</c> and so on.
/// </summary>
internal static class EstimateWorkflowInput
{
    public static Dictionary<string, object> ToDictionary(Estimate estimate)
    {
        var party = estimate.BillTo?.Party ?? default;

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["EstimateId"] = estimate.ItemId,
            ["Number"] = estimate.Number,
            ["Status"] = estimate.Status.ToString(),
            ["Currency"] = estimate.Currency,
            ["Total"] = estimate.Totals?.Total ?? 0m,
            ["BillToKind"] = party.IsEmpty ? null : party.Kind,
            ["BillToId"] = party.IsEmpty ? null : party.Id,
            ["BillToName"] = estimate.BillTo?.Contact?.Name,
            ["BillToEmail"] = estimate.BillTo?.Contact?.Email,
            ["ExpiryDate"] = estimate.ExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["AcceptedBy"] = estimate.Acceptance?.Name,
            ["DeclineReason"] = estimate.DeclineReason,
            ["InvoiceIds"] = estimate.InvoiceIds.ToArray(),
        };
    }
}

/// <summary>
/// Starts when an estimate is sent.
/// </summary>
public sealed class EstimateSentEvent : InvoiceEventActivity
{
    public EstimateSentEvent(IStringLocalizer<EstimateSentEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(EstimateSentEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Estimate Sent"];
}

/// <summary>
/// Starts when the customer accepts an estimate.
/// </summary>
public sealed class EstimateAcceptedEvent : InvoiceEventActivity
{
    public EstimateAcceptedEvent(IStringLocalizer<EstimateAcceptedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(EstimateAcceptedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Estimate Accepted"];
}

/// <summary>
/// Starts when the customer declines an estimate.
/// </summary>
public sealed class EstimateDeclinedEvent : InvoiceEventActivity
{
    public EstimateDeclinedEvent(IStringLocalizer<EstimateDeclinedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(EstimateDeclinedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Estimate Declined"];
}

/// <summary>
/// Starts when an estimate expires unanswered.
/// </summary>
public sealed class EstimateExpiredEvent : InvoiceEventActivity
{
    public EstimateExpiredEvent(IStringLocalizer<EstimateExpiredEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(EstimateExpiredEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Estimate Expired"];
}

/// <summary>
/// Starts the estimate workflow events after the request's work is saved.
/// </summary>
internal sealed class EstimateWorkflowObserver : IEstimateObserver
{
    private readonly IWorkflowManager _workflowManager;

    public EstimateWorkflowObserver(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    public Task SentAsync(Estimate estimate, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(EstimateSentEvent), estimate);

    public Task AcceptedAsync(Estimate estimate, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(EstimateAcceptedEvent), estimate);

    public Task DeclinedAsync(Estimate estimate, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(EstimateDeclinedEvent), estimate);

    public Task ExpiredAsync(Estimate estimate, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(EstimateExpiredEvent), estimate);

    private Task TriggerAsync(string eventName, Estimate estimate)
    {
        var input = EstimateWorkflowInput.ToDictionary(estimate);
        var correlationId = estimate.ItemId;

        if (ShellScope.Current is null)
        {
            return _workflowManager.TriggerEventAsync(eventName, input, correlationId);
        }

        DeferredWork.Add(InvoiceWorkflowObserver.WorkName, scope => scope.ServiceProvider
            .GetRequiredService<IWorkflowManager>()
            .TriggerEventAsync(eventName, input, correlationId));

        return Task.CompletedTask;
    }
}

/// <summary>
/// Publishes the estimate webhook events, each with a stable id.
/// </summary>
internal sealed class EstimateWebhookObserver : IEstimateObserver
{
    private readonly IWebhookPublisher _publisher;

    public EstimateWebhookObserver(IWebhookPublisher publisher)
    {
        _publisher = publisher;
    }

    public Task SentAsync(Estimate estimate, CancellationToken cancellationToken = default)
        => PublishAsync(InvoicingConstants.WebhookEvents.EstimateSent, estimate, null, $"evt_estimate-sent_{estimate.ItemId}", cancellationToken);

    public Task AcceptedAsync(Estimate estimate, CancellationToken cancellationToken = default)
        => PublishAsync(InvoicingConstants.WebhookEvents.EstimateAccepted, estimate, null, $"evt_estimate-accepted_{estimate.ItemId}", cancellationToken);

    public Task DeclinedAsync(Estimate estimate, CancellationToken cancellationToken = default)
        => PublishAsync(InvoicingConstants.WebhookEvents.EstimateDeclined, estimate, null, $"evt_estimate-declined_{estimate.ItemId}", cancellationToken);

    public Task ExpiredAsync(Estimate estimate, CancellationToken cancellationToken = default)
        => PublishAsync(InvoicingConstants.WebhookEvents.EstimateExpired, estimate, null, $"evt_estimate-expired_{estimate.ItemId}", cancellationToken);

    public Task ConvertedAsync(Estimate estimate, Invoice invoice, CancellationToken cancellationToken = default)
        => PublishAsync(InvoicingConstants.WebhookEvents.EstimateConverted, estimate, new { invoiceId = invoice.ItemId, kind = invoice.Kind.ToString() }, $"evt_estimate-converted_{estimate.ItemId}_{invoice.ItemId}", cancellationToken);

    private Task PublishAsync(string eventName, Estimate estimate, object details, string eventId, CancellationToken cancellationToken)
    {
        var party = estimate.BillTo?.Party ?? default;

        return _publisher.PublishAsync(eventName, new
        {
            estimate = new
            {
                id = estimate.ItemId,
                number = estimate.Number,
                status = estimate.Status.ToString(),
                currency = estimate.Currency,
                total = estimate.Totals?.Total ?? 0m,
                billToKind = party.IsEmpty ? null : party.Kind,
                billToId = party.IsEmpty ? null : party.Id,
                billToName = estimate.BillTo?.Contact?.Name,
                expiryDate = estimate.ExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                acceptedBy = estimate.Acceptance?.Name,
                invoiceIds = estimate.InvoiceIds.ToArray(),
            },
            details,
        }, eventId, cancellationToken);
    }
}

[Feature(InvoicingConstants.Features.Estimates)]
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class EstimateWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IEstimateObserver, EstimateWorkflowObserver>();
        services.AddActivity<EstimateSentEvent, InvoiceEventDisplayDriver<EstimateSentEvent>>();
        services.AddActivity<EstimateAcceptedEvent, InvoiceEventDisplayDriver<EstimateAcceptedEvent>>();
        services.AddActivity<EstimateDeclinedEvent, InvoiceEventDisplayDriver<EstimateDeclinedEvent>>();
        services.AddActivity<EstimateExpiredEvent, InvoiceEventDisplayDriver<EstimateExpiredEvent>>();
    }
}

[Feature(InvoicingConstants.Features.Estimates)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class EstimateWorkflowsRecipesSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<EstimateSentEventSchema>()
            .AddWorkflowActivitySchema<EstimateAcceptedEventSchema>()
            .AddWorkflowActivitySchema<EstimateDeclinedEventSchema>()
            .AddWorkflowActivitySchema<EstimateExpiredEventSchema>();
    }
}

[Feature(InvoicingConstants.Features.Estimates)]
[RequireFeatures(CommerceConstants.Features.Webhooks)]
public sealed class EstimateWebhooksStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IEstimateObserver, EstimateWebhookObserver>();
        services.AddWebhookEvent(InvoicingConstants.WebhookEvents.EstimateSent, "Estimate sent", "Invoicing");
        services.AddWebhookEvent(InvoicingConstants.WebhookEvents.EstimateAccepted, "Estimate accepted", "Invoicing");
        services.AddWebhookEvent(InvoicingConstants.WebhookEvents.EstimateDeclined, "Estimate declined", "Invoicing");
        services.AddWebhookEvent(InvoicingConstants.WebhookEvents.EstimateExpired, "Estimate expired", "Invoicing");
        services.AddWebhookEvent(InvoicingConstants.WebhookEvents.EstimateConverted, "Estimate invoiced", "Invoicing");
    }
}

internal sealed class EstimateSentEventSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "EstimateSentEvent";

    protected override string Category => "Invoicing";

    protected override string DisplayText => "Estimate Sent";

    protected override string Description => "Starts when an estimate is sent";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}

internal sealed class EstimateAcceptedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "EstimateAcceptedEvent";

    protected override string Category => "Invoicing";

    protected override string DisplayText => "Estimate Accepted";

    protected override string Description => "Starts when the customer accepts an estimate";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}

internal sealed class EstimateDeclinedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "EstimateDeclinedEvent";

    protected override string Category => "Invoicing";

    protected override string DisplayText => "Estimate Declined";

    protected override string Description => "Starts when the customer declines an estimate";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}

internal sealed class EstimateExpiredEventSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "EstimateExpiredEvent";

    protected override string Category => "Invoicing";

    protected override string DisplayText => "Estimate Expired";

    protected override string Description => "Starts when an estimate expires unanswered";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}

/// <summary>
/// Exports every estimate.
/// </summary>
public sealed class EstimatesDeploymentStep : DeploymentStep
{
    public const string StepName = "Estimates";

    public const string EntriesName = "Estimates";

    private static readonly LocalizationSource _category = LocalizationSource.Create<EstimatesDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<EstimatesDeploymentStep>("Estimates");

    public override string Name => StepName;

    [JsonIgnore]
    public override LocalizationSource Category => _category;

    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class EstimatesDeploymentSource : CrestApps.OrchardCore.Core.Deployments.CatalogDeploymentSourceBase<EstimatesDeploymentStep, Estimate>
{
    private const int PageSize = 500;

    private readonly IEstimateStore _estimates;

    public EstimatesDeploymentSource(IEstimateStore estimates)
        : base(EstimatesDeploymentStep.EntriesName)
    {
        _estimates = estimates;
    }

    protected override async Task<IEnumerable<Estimate>> GetEntriesAsync()
    {
        var entries = new List<Estimate>();

        for (var page = 1; ; page++)
        {
            var result = await _estimates.PageAsync(page, PageSize, new EstimateQuery());

            entries.AddRange(result.Entries);

            if (result.Entries.Count < PageSize)
            {
                return entries;
            }
        }
    }

    protected override JsonObject Export(Estimate entry)
        => InvoicingDocumentExport.Export(entry);
}

/// <summary>
/// Imports the estimates a recipe lists under <c>Estimates</c>: a sent estimate keeps its number, one the site already
/// sent is kept as it is, a draft is replaced.
/// </summary>
internal sealed class EstimatesStep : NamedRecipeStepHandler
{
    private readonly InvoicingDocumentImporter _importer;
    private readonly IEstimateStore _estimates;

    public EstimatesStep(InvoicingDocumentImporter importer, IEstimateStore estimates)
        : base(EstimatesDeploymentStep.StepName)
    {
        _importer = importer;
        _estimates = estimates;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var result = await _importer.ImportEstimatesAsync(CatalogRecipeSections.Section(context.Step, EstimatesDeploymentStep.EntriesName).Select(entry => (JsonObject)entry.DeepClone()), _estimates);

        foreach (var error in result.Errors)
        {
            context.Errors.Add(error);
        }
    }
}

internal sealed class EstimatesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(EstimatesDeploymentStep);

    protected override string DisplayText => "Estimates";

    protected override string Description => "Exports every estimate, drafts and sent, with its number, lines, totals, acceptance and history.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}

internal sealed class EstimatesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => EstimatesDeploymentStep.StepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(EstimatesDeploymentStep.StepName).Description("Recipe step discriminator. Must be 'Estimates'.")),
                (EstimatesDeploymentStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The estimate's 26-character id. A draft with this id is replaced; a sent estimate with this id is kept as it is.")),
                            ("Number", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The estimate's number, kept as it is. Required on a sent estimate.")),
                            ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Integer).Description("Draft, Sent, Accepted, Declined, Expired, Canceled or Invoiced.")),
                            ("ExpiryDate", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The last day it can be accepted, yyyy-MM-dd.")),
                            ("Lines", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The lines, as on an invoice.")))
                        .Required("ItemId")
                        .AdditionalProperties(true))
                    .Description("The estimates to import, as an Estimates deployment step writes them.")))
            .Required("name", EstimatesDeploymentStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}

[Feature(InvoicingConstants.Features.Estimates)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class EstimatesDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDeployment<EstimatesDeploymentSource, EstimatesDeploymentStep>();
}

[Feature(InvoicingConstants.Features.Estimates)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class EstimatesRecipeSchemasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, EstimatesRecipeStepSchema>();
        services.AddDeploymentStepSchema<EstimatesDeploymentStepSchema>();
    }
}

/// <summary>
/// One row per estimate: its number, status, total, who it is for, and its dates.
/// </summary>
public sealed class EstimatesReportDataSet : IndexedReportDataSet<Estimate, EstimateIndex>, IInvoicingReportDataSet
{
    public EstimatesReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<EstimatesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.InvoicingSource.Estimates,
                stringLocalizer["Estimates"],
                stringLocalizer["One row per estimate: number, status, total in its currency, who it is for, deposit, and its dates."]),
            session,
            authorizationService,
            InvoicingConstants.CollectionName,
            InvoicingPermissions.ViewInvoices)
    {
        var S = stringLocalizer;
        var estimate = S["Estimate"].Value;
        var money = S["Money"].Value;
        var dates = S["Dates"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Estimate ID"], DataFieldType.Text, record => record.ItemId, estimate).Identifier();
        AddField(nameof(Estimate.Number), S["Number"], DataFieldType.Text, record => record.Number, estimate);
        AddField(nameof(Estimate.Status), S["Status"], DataFieldType.Text, record => record.Status, estimate);
        AddField(nameof(Estimate.Revision), S["Revision"], DataFieldType.Integer, record => record.Revision, estimate);
        AddField(nameof(Estimate.Reference), S["Reference"], DataFieldType.Text, record => record.Reference, estimate);
        AddField("BillToId", S["Customer ID"], DataFieldType.Text, record => record.BillTo?.Party is { IsEmpty: false } party ? party.Id : null, estimate).Identifier();
        AddField("BillToName", S["Customer"], DataFieldType.Text, record => record.BillTo?.Contact?.Name, estimate);
        AddField("AcceptedBy", S["Accepted by"], DataFieldType.Text, record => record.Acceptance?.Name, estimate);
        AddField("Invoiced", S["Invoiced"], DataFieldType.Boolean, record => record.Status == EstimateStatus.Invoiced, estimate);

        AddField(nameof(Estimate.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, money);
        AddField("Total", S["Total"], DataFieldType.Decimal, record => record.Totals?.Total ?? 0m, money);
        AddField("TaxTotal", S["Tax"], DataFieldType.Decimal, record => record.Totals?.TaxTotal ?? 0m, money);
        AddField(nameof(Estimate.DepositPercent), S["Deposit (%)"], DataFieldType.Decimal, record => record.DepositPercent, money);
        AddField(nameof(Estimate.DepositAmount), S["Deposit (amount)"], DataFieldType.Decimal, record => record.DepositAmount, money);

        AddField("IssueDate", S["Date"], DataFieldType.Date, record => record.IssueDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), dates);
        AddField("ExpiryDate", S["Valid until"], DataFieldType.Date, record => record.ExpiryDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), dates);
        AddField("CreatedUtc", S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(Estimate.SentUtc), S["Sent"], DataFieldType.DateTime, record => record.SentUtc, dates);
        AddField(nameof(Estimate.ClosedUtc), S["Answered or expired"], DataFieldType.DateTime, record => record.ClosedUtc, dates);

        CompleteDescription();
    }

    protected override (string Field, System.Linq.Expressions.Expression<Func<EstimateIndex, DateTime>> Column)? DateColumn
        => ("CreatedUtc", index => index.CreatedUtc);
}

[Feature(InvoicingConstants.Features.Estimates)]
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class EstimatesDataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<IInvoicingReportDataSet, EstimatesReportDataSet>();
}
