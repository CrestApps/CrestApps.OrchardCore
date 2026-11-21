using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Core.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Learning.Coursework.Recipes;

/// <summary>
/// Exports coursework submissions, naming people by user name. Submissions are learner records: the step is opt-in and
/// needs the Export learner records permission.
/// </summary>
public sealed class LearningSubmissionsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<LearningSubmissionsDeploymentStep>("Learning");
    private static readonly LocalizationSource _title = LocalizationSource.Create<LearningSubmissionsDeploymentStep>("Learning coursework submissions (learner records)");

    /// <inheritdoc />
    public override string Name => CourseworkConstants.SubmissionsRecipeStep;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets the coursework to export, or <see langword="null"/> for every coursework item.
    /// </summary>
    public string CourseworkId { get; set; }
}

/// <summary>
/// The editor of the submissions deployment step.
/// </summary>
public class LearningSubmissionsDeploymentStepViewModel
{
    /// <summary>Gets or sets the coursework.</summary>
    public string CourseworkId { get; set; }

    /// <summary>Gets or sets the coursework items.</summary>
    [BindNever]
    public IList<SelectListItem> CourseworkOptions { get; set; } = [];
}

/// <summary>
/// Writes the submissions step, 500 submissions a page, with people named by user name. It writes nothing for a person
/// without the Export learner records permission, and tells the export observers what it wrote.
/// </summary>
internal sealed class LearningSubmissionsDeploymentSource : DeploymentSourceBase<LearningSubmissionsDeploymentStep>
{
    private const int PageSize = 500;

    private readonly SubmissionStore _store;
    private readonly LearnerUserLookup _userLookup;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<ILearnerRecordExportObserver> _exportObservers;
    private readonly ILogger _logger;

    public LearningSubmissionsDeploymentSource(
        SubmissionStore store,
        LearnerUserLookup userLookup,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IEnumerable<ILearnerRecordExportObserver> exportObservers,
        ILogger<LearningSubmissionsDeploymentSource> logger)
    {
        _store = store;
        _userLookup = userLookup;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _exportObservers = exportObservers;
        _logger = logger;
    }

    protected override async Task ProcessAsync(LearningSubmissionsDeploymentStep step, DeploymentPlanResult result)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        // A plan can run without a request, such as a scheduled remote deployment; then the plan's own check has passed.
        if (user?.Identity?.IsAuthenticated == true && !await _authorizationService.AuthorizeAsync(user, LearningPermissions.ExportLearnerRecords))
        {
            _logger.LogWarning("Coursework submissions were not exported: the user lacks the Export learner records permission.");

            return;
        }

        var rows = new JsonArray();

        for (var skip = 0; ; skip += PageSize)
        {
            var page = await _store.PageForExportAsync(step.CourseworkId, skip, PageSize);

            if (page.Count == 0)
            {
                break;
            }

            var names = await _userLookup.GetUserNamesAsync(page.Select(submission => submission.UserId)
                .Concat(page.Select(submission => submission.GraderId))
                .Concat(page.Select(submission => submission.ReturnedBy))
                .Concat(page.SelectMany(submission => submission.History.SelectMany(grade => new[] { grade.GraderId, grade.ReplacedBy })))
                .Where(id => !string.IsNullOrEmpty(id)));

            foreach (var submission in page)
            {
                var node = JsonSerializer.SerializeToNode(submission, LearningSubmissionsRecipeStep.JsonOptions).AsObject();

                node.Remove("userId");
                node["UserName"] = Name(names, submission.UserId);
                node["GraderUserName"] = Name(names, submission.GraderId);
                node["ReturnedByUserName"] = Name(names, submission.ReturnedBy);

                if (node["history"] is JsonArray history)
                {
                    var i = 0;

                    foreach (var grade in history.OfType<JsonObject>())
                    {
                        grade["GraderUserName"] = Name(names, submission.History[i].GraderId);
                        grade["ReplacedByUserName"] = Name(names, submission.History[i].ReplacedBy);
                        i++;
                    }
                }

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
            ["Submissions"] = rows,
        });

        foreach (var observer in _exportObservers)
        {
            await observer.ExportedAsync(step.Name, rows.Count, user?.FindFirstValue(ClaimTypes.NameIdentifier), step.CourseworkId is null ? null : "Coursework " + step.CourseworkId);
        }
    }

    private static string Name(IDictionary<string, string> names, string userId)
        => !string.IsNullOrEmpty(userId) && names.TryGetValue(userId, out var name) ? name : null;
}

internal sealed class LearningSubmissionsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, LearningSubmissionsDeploymentStep>
{
    private readonly ISession _session;

    public LearningSubmissionsDeploymentStepDisplayDriver(ISession session)
    {
        _session = session;
    }

    public override Task<IDisplayResult> DisplayAsync(LearningSubmissionsDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("LearningSubmissionsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("LearningSubmissionsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(LearningSubmissionsDeploymentStep step, BuildEditorContext context)
        => Initialize<LearningSubmissionsDeploymentStepViewModel>("LearningSubmissionsDeploymentStep_Fields_Edit", async model =>
        {
            model.CourseworkId = step.CourseworkId;
            model.CourseworkOptions = (await _session.QueryIndex<ContentItemIndex>(index => index.ContentType == CourseworkConstants.ContentTypes.Coursework && index.Latest)
                    .OrderBy(index => index.DisplayText)
                    .Take(500)
                    .ListAsync())
                .Select(index => new SelectListItem(index.DisplayText, index.ContentItemId, index.ContentItemId == step.CourseworkId))
                .ToList();
        }).Location("Content");

    public override async Task<IDisplayResult> UpdateAsync(LearningSubmissionsDeploymentStep step, UpdateEditorContext context)
    {
        var model = new LearningSubmissionsDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        step.CourseworkId = string.IsNullOrWhiteSpace(model.CourseworkId) ? null : model.CourseworkId;

        return Edit(step, context);
    }
}

internal sealed class LearningSubmissionsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(LearningSubmissionsDeploymentStep);

    protected override string DisplayText => "Learning coursework submissions (learner records)";

    protected override string Description => "Exports coursework submissions with their grades, naming people by user name. The files stay in the protected store. Needs the Export learner records permission.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("CourseworkId", DeploymentSchemaBuilders.String("The content item id of the coursework to export. Empty for every coursework item."));
    }
}
