using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Recipes;
using CrestApps.OrchardCore.Payroll.Services;
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
/// An opt-in deployment step: the final pay statements of the employers the user may export, to import elsewhere as
/// history.
/// </summary>
public sealed class PayHistoryDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<PayHistoryDeploymentStep>("Payroll");
    private static readonly LocalizationSource _title = LocalizationSource.Create<PayHistoryDeploymentStep>("Pay history");

    public override string Name => PayrollConstants.PayHistoryRecipeStepName;

    [JsonIgnore]
    public override LocalizationSource Category => _category;

    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class PayHistoryDeploymentSource : DeploymentSourceBase<PayHistoryDeploymentStep>
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = null, Converters = { new JsonStringEnumConverter() } };

    private readonly PayrollReportReader _reader;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public PayHistoryDeploymentSource(PayrollReportReader reader, IHttpContextAccessor httpContextAccessor, INotifier notifier, IHtmlLocalizer<PayHistoryDeploymentSource> htmlLocalizer)
    {
        _reader = reader;
        _httpContextAccessor = httpContextAccessor;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    protected override async Task ProcessAsync(PayHistoryDeploymentStep step, DeploymentPlanResult result)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        // People's pay leaves only for a user who may export it: without a user, nothing is written.
        if (user?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var scope = await _reader.GetScopeAsync(user, PayrollPermissions.ExportPayrollData);

        if (scope.IsEmpty)
        {
            await _notifier.WarningAsync(H["The pay history was left out of the plan: you may not export payroll data."]);

            return;
        }

        var statements = new JsonArray();

        foreach (var statement in (await _reader.ListStatementsAsync(scope, null, null)).OrderBy(statement => statement.PayDate).ThenBy(statement => statement.Number, StringComparer.Ordinal))
        {
            var node = JsonSerializer.SerializeToNode(statement, _options)!.AsObject();

            // A run, a disbursement or a check of this site means nothing elsewhere.
            node.Remove(nameof(PayStatement.RunId));

            if (node[nameof(PayStatement.Payments)] is JsonArray payments)
            {
                foreach (var payment in payments.OfType<JsonObject>())
                {
                    payment.Remove(nameof(PayStatementPayment.ReferenceId));
                    payment.Remove(nameof(PayStatementPayment.BankAccountId));
                }
            }

            statements.Add(node);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = PayrollConstants.PayHistoryRecipeStepName,
            [PayHistoryRecipeStep.EntriesName] = statements,
        });
    }
}

internal sealed class PayHistoryDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, PayHistoryDeploymentStep>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public PayHistoryDeploymentStepDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService, IStringLocalizer<PayHistoryDeploymentStepDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(PayHistoryDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("PayHistoryDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("PayHistoryDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(PayHistoryDeploymentStep step, BuildEditorContext context)
        => View("PayHistoryDeploymentStep_Fields_Edit", step).Location(l => l.Zone("Content", "1").Card(S["Pay History"], "10"));

    public override async Task<IDisplayResult> UpdateAsync(PayHistoryDeploymentStep step, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, PayrollPermissions.ExportPayrollData))
        {
            context.Updater.ModelState.AddModelError(Prefix, S["Only a user who may export payroll data for every employer can add this step."]);
        }

        return Edit(step, context);
    }
}

internal sealed class PayHistoryDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(PayHistoryDeploymentStep);

    protected override string DisplayText => "Pay history";

    protected override string Description => "Exports the final pay statements of the employers the user may export, for another site to import as history. Bank accounts, runs, disbursements and checks are left out.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
