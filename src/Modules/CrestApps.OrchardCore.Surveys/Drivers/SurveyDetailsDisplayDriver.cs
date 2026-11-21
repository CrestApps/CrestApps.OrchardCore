using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Edits the details of a survey on its access and settings page: title, name, notes and tags.
/// </summary>
internal sealed class SurveyDetailsDisplayDriver : DisplayDriver<Survey>
{
    /// <summary>
    /// The editor group of the access and settings page.
    /// </summary>
    public const string GroupId = "Access";

    private readonly SurveyStore _store;

    internal readonly IStringLocalizer S;

    public SurveyDetailsDisplayDriver(
        SurveyStore store,
        IStringLocalizer<SurveyDetailsDisplayDriver> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(Survey survey, BuildEditorContext context)
        => Initialize<SurveyDetailsEditViewModel>("SurveyDetails_Edit", model =>
        {
            model.Title = survey.Title;
            model.Name = survey.Name;
            model.Description = survey.Description;
            model.Tags = string.Join(", ", survey.Tags ?? []);
        }).Location("Content:1").OnGroup(GroupId);

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(Survey survey, UpdateEditorContext context)
    {
        if (context.GroupId != GroupId)
        {
            return null;
        }

        var model = new SurveyDetailsEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Title), S["Enter a title."]);
        }

        var name = model.Name?.Trim();

        if (!SurveyNames.IsValidName(name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["The technical name may hold only lowercase letters, digits and dashes."]);
        }
        else if (await _store.FindByNameAsync(name) is { } other && other.ItemId != survey.ItemId)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Another survey uses this technical name."]);
        }
        else
        {
            survey.Name = name;
        }

        survey.Title = model.Title?.Trim();
        survey.Description = model.Description?.Trim();
        survey.Tags = (model.Tags ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Edit(survey, context);
    }
}
