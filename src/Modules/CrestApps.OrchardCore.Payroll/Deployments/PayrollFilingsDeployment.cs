using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.Deployments;

/// <summary>
/// Exports the tax deposits and filings of the employers the user may export.
/// </summary>
public sealed class PayrollFilingsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<PayrollFilingsDeploymentStep>("Payroll");
    private static readonly LocalizationSource _title = LocalizationSource.Create<PayrollFilingsDeploymentStep>("Tax deposits and filings");

    /// <inheritdoc />
    public override string Name => PayrollConstants.FilingsRecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets whether each filing's people (names, addresses and wage statements) are written; only for a user
    /// who may see full tax ids.
    /// </summary>
    public bool IncludeSensitiveData { get; set; }
}

/// <summary>
/// The editor of the tax deposits and filings step.
/// </summary>
public class PayrollFilingsDeploymentStepViewModel
{
    public bool IncludeSensitiveData { get; set; }
}

internal sealed class PayrollFilingsDeploymentSource : DeploymentSourceBase<PayrollFilingsDeploymentStep>
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = null, Converters = { new JsonStringEnumConverter() } };

    private readonly ITaxDepositManager _deposits;
    private readonly IPayrollFilingManager _filings;
    private readonly IEmployerManager _employers;
    private readonly IPayrollScopeService _scope;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public PayrollFilingsDeploymentSource(
        ITaxDepositManager deposits,
        IPayrollFilingManager filings,
        IEmployerManager employers,
        IPayrollScopeService scope,
        IHttpContextAccessor httpContextAccessor,
        INotifier notifier,
        IHtmlLocalizer<PayrollFilingsDeploymentSource> htmlLocalizer)
    {
        _deposits = deposits;
        _filings = filings;
        _employers = employers;
        _scope = scope;
        _httpContextAccessor = httpContextAccessor;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    protected override async Task ProcessAsync(PayrollFilingsDeploymentStep step, DeploymentPlanResult result)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        // Rule B: payroll records leave only for a user who may export them; without a user, nothing is written.
        if (user?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var scope = await _scope.GetScopeAsync(user, PayrollPermissions.ExportPayrollData);

        if (scope.IsEmpty)
        {
            await _notifier.WarningAsync(H["The tax deposits and filings were left out of the plan: you may not export payroll data."]);

            return;
        }

        var deposits = new JsonArray();

        foreach (var employer in await _employers.GetAllAsync())
        {
            if (!scope.All && !scope.EmployerIds.Contains(employer.ItemId))
            {
                continue;
            }

            foreach (var deposit in await _deposits.ListAsync(employer.ItemId, DateOnly.MinValue, DateOnly.MaxValue))
            {
                deposits.Add(JsonSerializer.SerializeToNode(deposit, _options));
            }
        }

        var filings = new JsonArray();

        foreach (var filing in await _filings.ListByEmployersAsync(scope.All ? null : [.. scope.EmployerIds]))
        {
            var node = JsonSerializer.SerializeToNode(filing, _options)!.AsObject();

            node.Remove(nameof(PayrollFiling.Period));

            // People's wage statements are personal data: only with Include sensitive data, by one who may see tax ids.
            if (!step.IncludeSensitiveData || !await _scope.AuthorizeAsync(user, PayrollPermissions.ViewTaxIds, filing.EmployerId))
            {
                node.Remove(nameof(PayrollFiling.People));
            }

            filings.Add(node);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = PayrollConstants.TaxDepositsRecipeStepName,
            [TaxDepositsRecipeStep.EntriesName] = deposits,
        });
        result.Steps.Add(new JsonObject
        {
            ["name"] = PayrollConstants.FilingsRecipeStepName,
            [PayrollFilingsRecipeStep.EntriesName] = filings,
        });
    }
}

internal sealed class PayrollFilingsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, PayrollFilingsDeploymentStep>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public PayrollFilingsDeploymentStepDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService, IStringLocalizer<PayrollFilingsDeploymentStepDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(PayrollFilingsDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("PayrollFilingsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("PayrollFilingsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(PayrollFilingsDeploymentStep step, BuildEditorContext context)
        => Initialize<PayrollFilingsDeploymentStepViewModel>("PayrollFilingsDeploymentStep_Fields_Edit", model => model.IncludeSensitiveData = step.IncludeSensitiveData).Location(l => l.Zone("Content", "1").Card(S["Tax Deposits and Filings"], "10"));

    public override async Task<IDisplayResult> UpdateAsync(PayrollFilingsDeploymentStep step, UpdateEditorContext context)
    {
        var model = new PayrollFilingsDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, PayrollPermissions.ExportPayrollData))
        {
            context.Updater.ModelState.AddModelError(Prefix, S["Only a user who may export payroll data for every employer can add this step."]);
        }
        else if (model.IncludeSensitiveData && !await _authorizationService.AuthorizeAsync(user, PayrollPermissions.ViewTaxIds))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.IncludeSensitiveData), S["Only a user who may see full tax ids can export people's wage statements."]);
        }
        else
        {
            step.IncludeSensitiveData = model.IncludeSensitiveData;
        }

        return Edit(step, context);
    }
}

internal sealed class PayrollFilingsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(PayrollFilingsDeploymentStep);

    protected override string DisplayText => "Tax deposits and filings";

    protected override string Description => "Exports the tax deposits and filings of the employers the user may export. People's wage statements only with IncludeSensitiveData.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [("IncludeSensitiveData", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether each filing's people (names, addresses and wage statements) are written."))];
}

/// <summary>
/// Imports tax deposits, matched by id.
/// </summary>
internal sealed class TaxDepositsRecipeStep : CatalogRecipeStep<TaxDeposit>
{
    public const string EntriesName = "Deposits";

    public TaxDepositsRecipeStep(ITaxDepositManager manager)
        : base(PayrollConstants.TaxDepositsRecipeStepName, EntriesName, manager)
    {
    }
}

/// <summary>
/// Imports filings, matched by id.
/// </summary>
internal sealed class PayrollFilingsRecipeStep : CatalogRecipeStep<PayrollFiling>
{
    public const string EntriesName = "Filings";

    public PayrollFilingsRecipeStep(IPayrollFilingManager manager)
        : base(PayrollConstants.FilingsRecipeStepName, EntriesName, manager)
    {
    }
}

/// <summary>
/// Describes the "PayrollTaxDeposits" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class TaxDepositsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => PayrollConstants.TaxDepositsRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        static JsonSchemaBuilder Text(string description)
            => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(PayrollConstants.TaxDepositsRecipeStepName).Description("Recipe step discriminator. Must be 'PayrollTaxDeposits'.")),
                (TaxDepositsRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Description("The tax deposits to create, or update by id.")
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", Text("The deposit's 26-character id.")),
                            ("EmployerId", Text("The employer.")),
                            ("Jurisdiction", Text("The jurisdiction deposited to, such as US.")),
                            ("Group", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Withholding", "Unemployment", "NonPayrollWithholding")),
                            ("PeriodStart", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Date).Description("The first day of the period paid.")),
                            ("PeriodEnd", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Date).Description("The last day of the period paid.")),
                            ("Amount", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The amount deposited.")),
                            ("PaidOn", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Date).Description("The day it was deposited.")),
                            ("Reference", Text("The agency's confirmation number.")))
                        .Required("EmployerId", "Jurisdiction", "PeriodStart", "PeriodEnd", "Amount", "PaidOn")
                        .AdditionalProperties(true))))
            .Required("name", TaxDepositsRecipeStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}

/// <summary>
/// Describes the "PayrollFilings" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class PayrollFilingsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => PayrollConstants.FilingsRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        static JsonSchemaBuilder Text(string description)
            => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

        var boxes = new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(new JsonSchemaBuilder().Type(SchemaValueType.Number));

        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(PayrollConstants.FilingsRecipeStepName).Description("Recipe step discriminator. Must be 'PayrollFilings'.")),
                (PayrollFilingsRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Description("The filings to create, or update by id.")
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", Text("The filing's 26-character id.")),
                            ("EmployerId", Text("The employer.")),
                            ("Form", Text("The form, such as US-941 or US-W2.")),
                            ("Year", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                            ("Quarter", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("1 to 4 for a quarterly form, 0 for a year.")),
                            ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Draft", "Filed", "Amended")),
                            ("Boxes", boxes.Description("The form's boxes, by number.")),
                            ("People", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).Properties(
                                ("StaffMemberId", Text("The staff member.")),
                                ("ProfileId", Text("The pay profile.")),
                                ("Name", Text("The name as printed.")),
                                ("TaxIdHint", Text("The last characters of the tax id.")),
                                ("Boxes", boxes)))),
                            ("FiledOn", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.Date)),
                            ("Reference", Text("The agency's confirmation number.")),
                            ("AmendsId", Text("The filing this one corrects.")))
                        .Required("EmployerId", "Form", "Year")
                        .AdditionalProperties(true))))
            .Required("name", PayrollFilingsRecipeStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
