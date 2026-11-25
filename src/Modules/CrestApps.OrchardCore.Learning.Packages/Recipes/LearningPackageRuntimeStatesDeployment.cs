using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Learning.Packages.Recipes;

/// <summary>
/// Exports learners' runtime states on learning packages, naming people by user name. Runtime states are learner
/// records: the step is opt-in and needs the Export learner records permission.
/// </summary>
public sealed class LearningPackageRuntimeStatesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<LearningPackageRuntimeStatesDeploymentStep>("Learning");
    private static readonly LocalizationSource _title = LocalizationSource.Create<LearningPackageRuntimeStatesDeploymentStep>("Learning package runtime states (learner records)");

    /// <inheritdoc />
    public override string Name => PackagesConstants.RuntimeStatesRecipeStep;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

/// <summary>
/// Writes the runtime states step, 500 states a page, with learners named by user name. It writes nothing for a person
/// without the Export learner records permission, and tells the export observers what it wrote.
/// </summary>
internal sealed class LearningPackageRuntimeStatesDeploymentSource : DeploymentSourceBase<LearningPackageRuntimeStatesDeploymentStep>
{
    private const int PageSize = 500;

    private readonly PackageRuntimeStore _store;
    private readonly LearnerUserLookup _userLookup;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<ILearnerRecordExportObserver> _exportObservers;
    private readonly ILogger _logger;

    public LearningPackageRuntimeStatesDeploymentSource(
        PackageRuntimeStore store,
        LearnerUserLookup userLookup,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IEnumerable<ILearnerRecordExportObserver> exportObservers,
        ILogger<LearningPackageRuntimeStatesDeploymentSource> logger)
    {
        _store = store;
        _userLookup = userLookup;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _exportObservers = exportObservers;
        _logger = logger;
    }

    protected override async Task ProcessAsync(LearningPackageRuntimeStatesDeploymentStep step, DeploymentPlanResult result)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        // A plan can run without a request, such as a scheduled remote deployment; then the plan's own check has passed.
        if (user?.Identity?.IsAuthenticated == true && !await _authorizationService.AuthorizeAsync(user, LearningPermissions.ExportLearnerRecords))
        {
            _logger.LogWarning("Learning package runtime states were not exported: the user lacks the Export learner records permission.");

            return;
        }

        var rows = new JsonArray();

        for (var skip = 0; ; skip += PageSize)
        {
            var page = await _store.PageForExportAsync(skip, PageSize);

            if (page.Count == 0)
            {
                break;
            }

            var names = await _userLookup.GetUserNamesAsync(page.Select(state => state.UserId));

            foreach (var state in page)
            {
                var node = JsonSerializer.SerializeToNode(state, LearningPackageRuntimeStatesRecipeStep.JsonOptions).AsObject();

                node.Remove("userId");

                // A SCORM 2004 state carries its 2004 data only.
                if (PackagesConstants.Standards.IsScorm2004(state.Standard))
                {
                    node.Remove("cmi");
                }
                node["UserName"] = !string.IsNullOrEmpty(state.UserId) && names.TryGetValue(state.UserId, out var name) ? name : null;
                rows.Add(node);
            }

            if (page.Count < PageSize)
            {
                break;
            }
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["States"] = rows,
        });

        foreach (var observer in _exportObservers)
        {
            await observer.ExportedAsync(step.Name, rows.Count, user?.FindFirstValue(ClaimTypes.NameIdentifier), null);
        }
    }
}

internal sealed class LearningPackageRuntimeStatesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, LearningPackageRuntimeStatesDeploymentStep>
{
    internal readonly IStringLocalizer S;

    public LearningPackageRuntimeStatesDeploymentStepDisplayDriver(IStringLocalizer<LearningPackageRuntimeStatesDeploymentStepDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(LearningPackageRuntimeStatesDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("LearningPackageRuntimeStatesDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("LearningPackageRuntimeStatesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(LearningPackageRuntimeStatesDeploymentStep step, BuildEditorContext context)
        => View("LearningPackageRuntimeStatesDeploymentStep_Fields_Edit", step).Location(l => l.Zone("Content", "1").Card(S["Runtime States"], "10"));
}

internal sealed class LearningPackageRuntimeStatesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(LearningPackageRuntimeStatesDeploymentStep);

    protected override string DisplayText => "Learning package runtime states (learner records)";

    protected override string Description => "Exports learners' SCORM runtime states on learning packages, naming people by user name. The package files stay in the protected store. Needs the Export learner records permission.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
