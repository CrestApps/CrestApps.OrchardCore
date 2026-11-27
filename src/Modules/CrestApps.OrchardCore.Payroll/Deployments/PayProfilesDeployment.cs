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
/// Exports pay profiles and their pay items. Tax ids are left out unless the step includes sensitive data.
/// </summary>
public sealed class PayProfilesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<PayProfilesDeploymentStep>("Payroll");
    private static readonly LocalizationSource _title = LocalizationSource.Create<PayProfilesDeploymentStep>("Pay profiles and pay items");

    /// <inheritdoc />
    public override string Name => PayrollConstants.PayProfilesRecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets whether tax ids are written, in clear.
    /// </summary>
    public bool IncludeSensitiveData { get; set; }
}

/// <summary>
/// The editor of the pay profiles step.
/// </summary>
public class PayProfilesDeploymentStepViewModel
{
    public bool IncludeSensitiveData { get; set; }
}

internal sealed class PayProfilesDeploymentSource : DeploymentSourceBase<PayProfilesDeploymentStep>
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = null, Converters = { new JsonStringEnumConverter() } };

    private readonly IPayProfileManager _profiles;
    private readonly IPayItemManager _items;
    private readonly IPayProfileService _service;
    private readonly IPayrollScopeService _scope;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public PayProfilesDeploymentSource(
        IPayProfileManager profiles,
        IPayItemManager items,
        IPayProfileService service,
        IPayrollScopeService scope,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<PayProfilesDeploymentSource> htmlLocalizer,
        IStringLocalizer<PayProfilesDeploymentSource> stringLocalizer)
    {
        _profiles = profiles;
        _items = items;
        _service = service;
        _scope = scope;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    protected override async Task ProcessAsync(PayProfilesDeploymentStep step, DeploymentPlanResult result)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        // People's pay data leaves only for a user who may export it: without a user, nothing is written.
        if (user?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var scope = await _scope.GetScopeAsync(user, PayrollPermissions.ExportPayrollData);

        if (scope.IsEmpty)
        {
            await _notifier.WarningAsync(H["The pay profiles were left out of the plan: you may not export payroll data."]);

            return;
        }

        var profiles = await _profiles.ListByEmployersAsync(scope.All ? null : [.. scope.EmployerIds]);
        var profileNodes = new JsonArray();
        var itemNodes = new JsonArray();

        foreach (var profile in profiles.OrderBy(profile => profile.CreatedUtc))
        {
            var node = JsonSerializer.SerializeToNode(profile, _options)!.AsObject();

            node.Remove(nameof(EmployeePayProfile.TaxId));
            node.Remove(nameof(EmployeePayProfile.TaxIdHash));

            if (step.IncludeSensitiveData && await _scope.AuthorizeAsync(user, PayrollPermissions.ViewTaxIds, profile.EmployerId))
            {
                node["TaxId"] = await _service.RevealTaxIdAsync(profile, S["Exported in a deployment plan"]);
            }

            profileNodes.Add(node);

            foreach (var item in await _items.ListByProfileAsync(profile.ItemId))
            {
                itemNodes.Add(JsonSerializer.SerializeToNode(item, _options));
            }
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = PayrollConstants.PayProfilesRecipeStepName,
            [PayProfilesRecipeStep.EntriesName] = profileNodes,
        });
        result.Steps.Add(new JsonObject
        {
            ["name"] = PayrollConstants.PayItemsRecipeStepName,
            [PayItemsRecipeStep.EntriesName] = itemNodes,
        });
    }
}

internal sealed class PayProfilesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, PayProfilesDeploymentStep>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public PayProfilesDeploymentStepDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService, IStringLocalizer<PayProfilesDeploymentStepDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(PayProfilesDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("PayProfilesDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("PayProfilesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(PayProfilesDeploymentStep step, BuildEditorContext context)
        => Initialize<PayProfilesDeploymentStepViewModel>("PayProfilesDeploymentStep_Fields_Edit", model => model.IncludeSensitiveData = step.IncludeSensitiveData).Location(l => l.Zone("Content", "1").Card(S["Pay Profiles and Pay Items"], "10"));

    public override async Task<IDisplayResult> UpdateAsync(PayProfilesDeploymentStep step, UpdateEditorContext context)
    {
        var model = new PayProfilesDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, PayrollPermissions.ExportPayrollData))
        {
            context.Updater.ModelState.AddModelError(Prefix, S["Only a user who may export payroll data for every employer can add this step."]);
        }
        else if (model.IncludeSensitiveData && !await _authorizationService.AuthorizeAsync(user, PayrollPermissions.ViewTaxIds))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.IncludeSensitiveData), S["Only a user who may see full tax ids can export them."]);
        }
        else
        {
            step.IncludeSensitiveData = model.IncludeSensitiveData;
        }

        return Edit(step, context);
    }
}

internal sealed class PayProfilesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(PayProfilesDeploymentStep);

    protected override string DisplayText => "Pay profiles and pay items";

    protected override string Description => "Exports the pay profiles and pay items of the employers the user may export. Tax ids only with IncludeSensitiveData.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [("IncludeSensitiveData", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether tax ids are written in clear."))];
}
