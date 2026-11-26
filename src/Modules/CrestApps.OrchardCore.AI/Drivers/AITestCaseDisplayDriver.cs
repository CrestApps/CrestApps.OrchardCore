using CrestApps.Core;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Displays an AI test case in the admin list and edits it.
/// </summary>
public sealed class AITestCaseDisplayDriver : DisplayDriver<AITestCase>
{
    private readonly IAIProfileStore _profileStore;
    private readonly IAIDeploymentManager _deploymentManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AITestCaseDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileStore">The AI profile store.</param>
    /// <param name="deploymentManager">The AI deployment manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AITestCaseDisplayDriver(
        IAIProfileStore profileStore,
        IAIDeploymentManager deploymentManager,
        IStringLocalizer<AITestCaseDisplayDriver> stringLocalizer)
    {
        _profileStore = profileStore;
        _deploymentManager = deploymentManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(AITestCase testCase, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = testCase.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("AITestCase_Checkbox_SummaryAdmin", testCase)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Checkbox:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Select:1"),
            View("AITestCase_Fields_SummaryAdmin", testCase)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("AITestCase_DefaultTags_SummaryAdmin", testCase)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Result:5"),
            View("AITestCase_DefaultMeta_SummaryAdmin", testCase)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "LastRun:5"),
            View("AITestCase_Buttons_SummaryAdmin", testCase).Location("Actions:5"),
            View("AITestCase_ActionsMenu_SummaryAdmin", testCase).Location("ActionsMenu:10"));
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AITestCase testCase, BuildEditorContext context)
    {
        return Combine(
            Initialize<AITestCaseViewModel>("AITestCaseFields_Edit", async model =>
            {
                model.Name = testCase.Name;
                model.ProfileId = testCase.ProfileId;
                model.ProfileVersion = testCase.ProfileVersion;
                model.Profiles = (await _profileStore.GetByTypeAsync(AIProfileType.Chat))
                    .Where(profile => !profile.Has<AIProfileVersionCopy>())
                    .OrderBy(profile => profile.DisplayText, StringComparer.CurrentCultureIgnoreCase)
                    .Select(profile => new SelectListItem(profile.DisplayText ?? profile.Name, profile.ItemId))
                    .ToList();
            }).Location(l => l.Zone("Content", "1").Card(S["General"], "10")),
            Initialize<AITestCaseViewModel>("AITestCaseScenario_Edit", model =>
            {
                model.Scenario = testCase.Scenario;
                model.Variables = FormatPairs(testCase.Variables);
                model.MockedTools = FormatPairs(testCase.MockedTools);
            }).Location(l => l.Zone("Content", "1").Card(S["Scenario"], "20")),
            Initialize<AITestCaseViewModel>("AITestCaseEvaluation_Edit", async model =>
            {
                model.Criteria = testCase.Criteria;
                model.MaxTurns = testCase.MaxTurns;
                model.PassingScore = testCase.PassingScore;
                model.CustomerDeploymentName = testCase.CustomerDeploymentName;
                model.JudgeDeploymentName = testCase.JudgeDeploymentName;
                model.Deployments = (await _deploymentManager.GetAllBySlotAsync(AIDeploymentSlotNames.Chat))
                    .Where(deployment => !string.IsNullOrWhiteSpace(deployment.Name))
                    .OrderBy(deployment => deployment.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(deployment => new SelectListItem(deployment.Name, deployment.Name))
                    .ToList();
            }).Location(l => l.Zone("Content", "1").Card(S["Evaluation"], "30")));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AITestCase testCase, UpdateEditorContext context)
    {
        var model = new AITestCaseViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        testCase.Name = model.Name?.Trim();
        testCase.ProfileId = model.ProfileId;
        testCase.ProfileVersion = model.ProfileVersion;
        testCase.Scenario = model.Scenario?.Trim();
        testCase.Criteria = model.Criteria?.Trim();
        testCase.Variables = ParsePairs(model.Variables);
        testCase.MockedTools = ParsePairs(model.MockedTools);
        testCase.MaxTurns = model.MaxTurns;
        testCase.PassingScore = model.PassingScore;
        testCase.CustomerDeploymentName = string.IsNullOrWhiteSpace(model.CustomerDeploymentName) ? null : model.CustomerDeploymentName;
        testCase.JudgeDeploymentName = string.IsNullOrWhiteSpace(model.JudgeDeploymentName) ? null : model.JudgeDeploymentName;

        foreach (var error in AITestCaseValidator.Validate(testCase, S))
        {
            context.Updater.ModelState.AddModelError(Prefix, error.MemberNames.First(), error.ErrorMessage);
        }

        return Edit(testCase, context);
    }

    /// <summary>
    /// Reads <c>name: value</c> lines into a dictionary; a line without a colon is ignored.
    /// </summary>
    /// <param name="text">The lines.</param>
    /// <returns>The pairs.</returns>
    internal static Dictionary<string, string> ParsePairs(string text)
    {
        var pairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in (text ?? string.Empty).Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = line.IndexOf(':');

            if (colon > 0 && line[..colon].Trim().Length > 0)
            {
                pairs[line[..colon].Trim()] = line[(colon + 1)..].Trim();
            }
        }

        return pairs;
    }

    /// <summary>
    /// Writes a dictionary as <c>name: value</c> lines.
    /// </summary>
    /// <param name="pairs">The pairs.</param>
    /// <returns>The lines.</returns>
    internal static string FormatPairs(IDictionary<string, string> pairs)
        => pairs is null ? string.Empty : string.Join('\n', pairs.Select(pair => $"{pair.Key}: {pair.Value}"));
}
