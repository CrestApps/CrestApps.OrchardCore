using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Builds the survey choices of the Surveys deployment step editors.
/// </summary>
internal static class SurveysDeploymentOptions
{
    /// <summary>
    /// Lists the surveys as choices.
    /// </summary>
    /// <param name="store">The survey store.</param>
    /// <param name="selected">The selected names.</param>
    /// <returns>The choices.</returns>
    public static async Task<IList<SelectListItem>> SurveysAsync(SurveyStore store, string[] selected)
        => (await store.GetAllAsync())
            .OrderBy(survey => survey.Title ?? survey.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(survey => new SelectListItem(survey.Title ?? survey.Name, survey.Name, selected?.Contains(survey.Name, StringComparer.OrdinalIgnoreCase) == true))
            .ToList();

    /// <summary>
    /// Cleans the selected names.
    /// </summary>
    /// <param name="names">The posted names.</param>
    /// <returns>The names.</returns>
    public static string[] Clean(string[] names)
        => (names ?? []).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}

/// <summary>
/// Shows and edits the Surveys deployment step.
/// </summary>
internal sealed class SurveysDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, SurveysDeploymentStep>
{
    private readonly SurveyStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="store">The survey store.</param>
    public SurveysDeploymentStepDisplayDriver(SurveyStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public override IDisplayResult Display(SurveysDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("SurveysDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("SurveysDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveysDeploymentStep step, BuildEditorContext context)
        => Initialize<SurveysDeploymentStepViewModel>("SurveysDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Names = step.SurveyNames ?? [];
            model.IncludeSecrets = step.IncludeSecrets;
            model.Options = await SurveysDeploymentOptions.SurveysAsync(_store, model.Names);
        }).Location("Content");

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveysDeploymentStep step, UpdateEditorContext context)
    {
        var model = new SurveysDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, value => value.IncludeAll, value => value.Names, value => value.IncludeSecrets);

        step.IncludeAll = model.IncludeAll;
        step.SurveyNames = model.IncludeAll ? [] : SurveysDeploymentOptions.Clean(model.Names);
        step.IncludeSecrets = model.IncludeSecrets;

        return Edit(step, context);
    }
}

/// <summary>
/// Shows and edits the question banks deployment step.
/// </summary>
internal sealed class SurveyQuestionBanksDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, SurveyQuestionBanksDeploymentStep>
{
    private readonly QuestionBankStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyQuestionBanksDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="store">The bank store.</param>
    public SurveyQuestionBanksDeploymentStepDisplayDriver(QuestionBankStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public override IDisplayResult Display(SurveyQuestionBanksDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("SurveyQuestionBanksDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("SurveyQuestionBanksDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyQuestionBanksDeploymentStep step, BuildEditorContext context)
        => Initialize<SurveysDeploymentStepViewModel>("SurveyQuestionBanksDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Names = step.BankNames ?? [];
            model.Options = (await _store.GetAllAsync())
                .OrderBy(bank => bank.Title ?? bank.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(bank => new SelectListItem(bank.Title ?? bank.Name, bank.Name, model.Names.Contains(bank.Name, StringComparer.OrdinalIgnoreCase)))
                .ToList();
        }).Location("Content");

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveyQuestionBanksDeploymentStep step, UpdateEditorContext context)
    {
        var model = new SurveysDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, value => value.IncludeAll, value => value.Names);

        step.IncludeAll = model.IncludeAll;
        step.BankNames = model.IncludeAll ? [] : SurveysDeploymentOptions.Clean(model.Names);

        return Edit(step, context);
    }
}

/// <summary>
/// Shows and edits the survey responses deployment step.
/// </summary>
internal sealed class SurveyResponsesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, SurveyResponsesDeploymentStep>
{
    private readonly SurveyStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponsesDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="store">The survey store.</param>
    public SurveyResponsesDeploymentStepDisplayDriver(SurveyStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public override IDisplayResult Display(SurveyResponsesDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("SurveyResponsesDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("SurveyResponsesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyResponsesDeploymentStep step, BuildEditorContext context)
        => Initialize<SurveysDeploymentStepViewModel>("SurveyResponsesDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Names = step.SurveyNames ?? [];
            model.FromDate = step.FromDate;
            model.ToDate = step.ToDate;
            model.IncludeTest = step.IncludeTest;
            model.IncludeInProgress = step.IncludeInProgress;
            model.Options = await SurveysDeploymentOptions.SurveysAsync(_store, model.Names);
        }).Location("Content");

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveyResponsesDeploymentStep step, UpdateEditorContext context)
    {
        var model = new SurveysDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            value => value.IncludeAll,
            value => value.Names,
            value => value.FromDate,
            value => value.ToDate,
            value => value.IncludeTest,
            value => value.IncludeInProgress);

        step.IncludeAll = model.IncludeAll;
        step.SurveyNames = model.IncludeAll ? [] : SurveysDeploymentOptions.Clean(model.Names);
        step.FromDate = model.FromDate;
        step.ToDate = model.ToDate;
        step.IncludeTest = model.IncludeTest;
        step.IncludeInProgress = model.IncludeInProgress;

        return Edit(step, context);
    }
}

/// <summary>
/// Shows and edits the survey accommodations deployment step.
/// </summary>
internal sealed class SurveyOverridesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, SurveyOverridesDeploymentStep>
{
    private readonly SurveyStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyOverridesDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="store">The survey store.</param>
    public SurveyOverridesDeploymentStepDisplayDriver(SurveyStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public override IDisplayResult Display(SurveyOverridesDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("SurveyOverridesDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("SurveyOverridesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyOverridesDeploymentStep step, BuildEditorContext context)
        => Initialize<SurveysDeploymentStepViewModel>("SurveyOverridesDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Names = step.SurveyNames ?? [];
            model.Options = await SurveysDeploymentOptions.SurveysAsync(_store, model.Names);
        }).Location("Content");

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveyOverridesDeploymentStep step, UpdateEditorContext context)
    {
        var model = new SurveysDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, value => value.IncludeAll, value => value.Names);

        step.IncludeAll = model.IncludeAll;
        step.SurveyNames = model.IncludeAll ? [] : SurveysDeploymentOptions.Clean(model.Names);

        return Edit(step, context);
    }
}

/// <summary>
/// Shows and edits the survey invitations deployment step.
/// </summary>
internal sealed class SurveyInvitationsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, SurveyInvitationsDeploymentStep>
{
    private readonly SurveyStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationsDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="store">The survey store.</param>
    public SurveyInvitationsDeploymentStepDisplayDriver(SurveyStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public override IDisplayResult Display(SurveyInvitationsDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("SurveyInvitationsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("SurveyInvitationsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyInvitationsDeploymentStep step, BuildEditorContext context)
        => Initialize<SurveysDeploymentStepViewModel>("SurveyInvitationsDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Names = step.SurveyNames ?? [];
            model.Options = await SurveysDeploymentOptions.SurveysAsync(_store, model.Names);
        }).Location("Content");

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveyInvitationsDeploymentStep step, UpdateEditorContext context)
    {
        var model = new SurveysDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, value => value.IncludeAll, value => value.Names);

        step.IncludeAll = model.IncludeAll;
        step.SurveyNames = model.IncludeAll ? [] : SurveysDeploymentOptions.Clean(model.Names);

        return Edit(step, context);
    }
}
