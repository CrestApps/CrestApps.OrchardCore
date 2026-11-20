using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
/// Exports garnishment orders. Case numbers and payee bank accounts are left out unless the step includes sensitive data.
/// </summary>
public sealed class GarnishmentsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<GarnishmentsDeploymentStep>("Payroll");
    private static readonly LocalizationSource _title = LocalizationSource.Create<GarnishmentsDeploymentStep>("Garnishment orders");

    /// <inheritdoc />
    public override string Name => PayrollConstants.GarnishmentsRecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets whether case numbers and payee bank accounts are written.
    /// </summary>
    public bool IncludeSensitiveData { get; set; }
}

/// <summary>
/// The editor of the garnishments step.
/// </summary>
public class GarnishmentsDeploymentStepViewModel
{
    public bool IncludeSensitiveData { get; set; }
}

internal sealed class GarnishmentsDeploymentSource : DeploymentSourceBase<GarnishmentsDeploymentStep>
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = null, Converters = { new JsonStringEnumConverter() } };

    private readonly IGarnishmentOrderManager _orders;
    private readonly IPayrollScopeService _scope;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public GarnishmentsDeploymentSource(
        IGarnishmentOrderManager orders,
        IPayrollScopeService scope,
        IHttpContextAccessor httpContextAccessor,
        INotifier notifier,
        IHtmlLocalizer<GarnishmentsDeploymentSource> htmlLocalizer)
    {
        _orders = orders;
        _scope = scope;
        _httpContextAccessor = httpContextAccessor;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    protected override async Task ProcessAsync(GarnishmentsDeploymentStep step, DeploymentPlanResult result)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        // Rule B: people's orders leave only for a user who may export them; without a user, nothing is written.
        if (user?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var scope = await _scope.GetScopeAsync(user, PayrollPermissions.ExportPayrollData);

        if (scope.IsEmpty)
        {
            await _notifier.WarningAsync(H["The garnishment orders were left out of the plan: you may not export payroll data."]);

            return;
        }

        var nodes = new JsonArray();

        foreach (var order in await _orders.ListByEmployersAsync(scope.All ? null : [.. scope.EmployerIds]))
        {
            var node = JsonSerializer.SerializeToNode(order, _options)!.AsObject();

            node.Remove(nameof(GarnishmentOrder.Remaining));

            // What was sent belongs to this site's statements and payments.
            node.Remove(nameof(GarnishmentOrder.Remittances));
            node.Remove(nameof(GarnishmentOrder.Withheld));

            if (!step.IncludeSensitiveData)
            {
                node.Remove(nameof(GarnishmentOrder.CaseNumber));
                node.Remove(nameof(GarnishmentOrder.PayeeBankAccountId));
            }

            nodes.Add(node);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = PayrollConstants.GarnishmentsRecipeStepName,
            [GarnishmentsRecipeStep.EntriesName] = nodes,
        });
    }
}

internal sealed class GarnishmentsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, GarnishmentsDeploymentStep>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public GarnishmentsDeploymentStepDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService, IStringLocalizer<GarnishmentsDeploymentStepDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(GarnishmentsDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("GarnishmentsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("GarnishmentsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(GarnishmentsDeploymentStep step, BuildEditorContext context)
        => Initialize<GarnishmentsDeploymentStepViewModel>("GarnishmentsDeploymentStep_Fields_Edit", model => model.IncludeSensitiveData = step.IncludeSensitiveData).Location("Content");

    public override async Task<IDisplayResult> UpdateAsync(GarnishmentsDeploymentStep step, UpdateEditorContext context)
    {
        var model = new GarnishmentsDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, PayrollPermissions.ExportPayrollData))
        {
            context.Updater.ModelState.AddModelError(Prefix, S["Only a user who may export payroll data for every employer can add this step."]);
        }
        else
        {
            step.IncludeSensitiveData = model.IncludeSensitiveData;
        }

        return Edit(step, context);
    }
}

internal sealed class GarnishmentsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(GarnishmentsDeploymentStep);

    protected override string DisplayText => "Garnishment orders";

    protected override string Description => "Exports the garnishment orders of the employers the user may export. Case numbers and payee bank accounts only with IncludeSensitiveData.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [("IncludeSensitiveData", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether case numbers and payee bank accounts are written."))];
}
