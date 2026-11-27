using System.Globalization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Tickets.Workflows;

/// <summary>
/// The editor of a ticket event: the desks it starts for, and the settings of the events that have their own (changed
/// fields, status categories, an action's name, the time-elapsed settings).
/// </summary>
/// <typeparam name="TActivity">The event.</typeparam>
public sealed class TicketEventDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, TicketEventViewModel>
    where TActivity : TicketEventActivity
{
    private readonly ICatalog<TicketDesk> _desks;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ITicketCategoryService _categories;

    internal readonly IStringLocalizer S;

    public TicketEventDisplayDriver(
        ICatalog<TicketDesk> desks,
        IContentDefinitionManager contentDefinitionManager,
        ITicketCategoryService categories,
        IStringLocalizer<TicketEventViewModel> stringLocalizer)
    {
        _desks = desks;
        _contentDefinitionManager = contentDefinitionManager;
        _categories = categories;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TActivity activity, BuildEditorContext context)
    {
        return Initialize<TicketEventViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Settings"], "10"));
    }

    protected override async ValueTask EditActivityAsync(TActivity activity, TicketEventViewModel model)
    {
        model.DeskIds = [.. activity.DeskIds];
        model.DeskOptions = [.. (await _desks.GetAllAsync()).OrderBy(desk => desk.Name, StringComparer.CurrentCultureIgnoreCase).Select(desk => new SelectListItem(desk.Name, desk.ItemId, model.DeskIds.Contains(desk.ItemId)))];
        model.TicketTypes = [.. activity.TicketTypes];
        model.CategoryIds = [.. activity.CategoryIds];

        var types = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(type => type.Parts.Any(part => part.PartDefinition.Name == TicketsConstants.PartName))
            .OrderBy(type => type.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        model.TicketTypeOptions = [.. types.Select(type => new SelectListItem(type.DisplayName, type.Name, model.TicketTypes.Contains(type.Name)))];

        // The categories of every ticket type, each once, in tree order, by their path's names.
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in types)
        {
            foreach (var category in await _categories.ListAsync(type.Name))
            {
                if (seen.Add(category.Id))
                {
                    model.CategoryOptions.Add(new SelectListItem(category.PathText ?? category.Name, category.Id, model.CategoryIds.Contains(category.Id)));
                }
            }
        }

        switch (activity)
        {
            case TicketUpdatedEvent updated:
                model.ShowChangedFields = true;
                model.ChangedFields = [.. updated.ChangedFields];
                break;
            case TicketStatusChangedEvent statusChanged:
                model.ShowCategories = true;
                model.Categories = [.. statusChanged.Categories];
                break;
            case TicketActionRequestedEvent action:
                model.ShowActionName = true;
                model.ActionName = action.ActionName;
                break;
            case TicketTimeElapsedEvent elapsed:
                model.ShowTimeElapsed = true;
                model.ShowCategories = true;
                model.Categories = [.. elapsed.Categories];
                model.Hours = elapsed.Hours.ToString(CultureInfo.InvariantCulture);
                model.Since = elapsed.Since;
                model.BusinessHours = elapsed.BusinessHours;
                break;
        }
    }

    public override async Task<IDisplayResult> UpdateAsync(TActivity activity, UpdateEditorContext context)
    {
        var model = new TicketEventViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        activity.DeskIds = Clean(model.DeskIds);
        activity.TicketTypes = Clean(model.TicketTypes);
        activity.CategoryIds = Clean(model.CategoryIds);

        switch (activity)
        {
            case TicketUpdatedEvent updated:
                updated.ChangedFields = Clean(model.ChangedFields);
                break;
            case TicketStatusChangedEvent statusChanged:
                statusChanged.Categories = Clean(model.Categories);
                break;
            case TicketActionRequestedEvent action:
                action.ActionName = model.ActionName?.Trim();
                if (string.IsNullOrEmpty(action.ActionName))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ActionName), S["Name the action."]);
                }

                break;
            case TicketTimeElapsedEvent elapsed:
                elapsed.Categories = Clean(model.Categories);
                elapsed.Since = model.Since ?? TicketTimeElapsedSince.Created;
                elapsed.BusinessHours = model.BusinessHours;
                if (double.TryParse(model.Hours, NumberStyles.Float, CultureInfo.InvariantCulture, out var hours) && hours > 0)
                {
                    elapsed.Hours = hours;
                }
                else
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.Hours), S["Give a number of hours above 0."]);
                }

                break;
        }

        return Edit(activity, context);
    }

    private static List<string> Clean(IEnumerable<string> values)
        => [.. (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal)];
}

/// <summary>
/// The editor of a ticket event.
/// </summary>
public class TicketEventViewModel
{
    public IList<string> DeskIds { get; set; } = [];

    public IList<string> TicketTypes { get; set; } = [];

    public IList<string> CategoryIds { get; set; } = [];

    public IList<SelectListItem> TicketTypeOptions { get; set; } = [];

    public IList<SelectListItem> CategoryOptions { get; set; } = [];

    public IList<string> ChangedFields { get; set; } = [];

    public IList<string> Categories { get; set; } = [];

    public string ActionName { get; set; }

    public string Hours { get; set; }

    public string Since { get; set; }

    public bool BusinessHours { get; set; }

    [BindNever]
    public IList<SelectListItem> DeskOptions { get; set; } = [];

    [BindNever]
    public bool ShowChangedFields { get; set; }

    [BindNever]
    public bool ShowCategories { get; set; }

    [BindNever]
    public bool ShowActionName { get; set; }

    [BindNever]
    public bool ShowTimeElapsed { get; set; }
}

/// <summary>
/// The editor of a ticket task: the ticket it works on, and its own fields.
/// </summary>
/// <typeparam name="TActivity">The task.</typeparam>
public sealed class TicketTaskDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, TicketTaskViewModel>
    where TActivity : TicketTaskActivity
{
    private readonly ICatalog<TicketDesk> _desks;
    private readonly ICatalog<TicketStatus> _statuses;
    private readonly ICatalog<TicketPriority> _priorities;

    internal readonly IStringLocalizer S;

    public TicketTaskDisplayDriver(
        ICatalog<TicketDesk> desks,
        ICatalog<TicketStatus> statuses,
        ICatalog<TicketPriority> priorities,
        IStringLocalizer<TicketTaskViewModel> stringLocalizer)
    {
        _desks = desks;
        _statuses = statuses;
        _priorities = priorities;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TActivity activity, BuildEditorContext context)
    {
        return Initialize<TicketTaskViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Settings"], "10"));
    }

    protected override async ValueTask EditActivityAsync(TActivity activity, TicketTaskViewModel model)
    {
        model.NeedsTicket = activity.NeedsTicket;
        model.TicketIdExpression = activity.TicketIdExpression;
        model.Fields = [];

        foreach (var field in activity.Fields)
        {
            var value = activity.GetValue(field.Name);

            model.Fields.Add(new TicketTaskFieldViewModel
            {
                Name = field.Name,
                Label = field.Label,
                Hint = field.Hint,
                Kind = field.Kind,
                Value = value,
                Options = field.Kind switch
                {
                    TicketTaskFieldKind.Status => [.. (await _statuses.GetAllAsync()).OrderBy(status => status.Order).Select(status => new SelectListItem(status.Name, status.ItemId, status.ItemId == value))],
                    TicketTaskFieldKind.Priority => [.. (await _priorities.GetAllAsync()).OrderByDescending(priority => priority.Rank).Select(priority => new SelectListItem(priority.Name, priority.ItemId, priority.ItemId == value))],
                    TicketTaskFieldKind.Desk => [.. (await _desks.GetAllAsync()).OrderBy(desk => desk.Name, StringComparer.CurrentCultureIgnoreCase).Select(desk => new SelectListItem(desk.Name, desk.ItemId, desk.ItemId == value))],
                    TicketTaskFieldKind.Choice => [.. (field.Choices ?? []).Select(choice => new SelectListItem(choice.Value, choice.Key, choice.Key == (value ?? string.Empty)))],
                    _ => [],
                },
            });
        }
    }

    public override async Task<IDisplayResult> UpdateAsync(TActivity activity, UpdateEditorContext context)
    {
        var model = new TicketTaskViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (activity.NeedsTicket)
        {
            activity.TicketIdExpression = string.IsNullOrWhiteSpace(model.TicketIdExpression) ? TicketTaskActivity.DefaultTicketIdExpression : model.TicketIdExpression.Trim();
        }

        var posted = (model.Fields ?? []).Where(field => !string.IsNullOrEmpty(field?.Name)).ToDictionary(field => field.Name, field => field.Value, StringComparer.Ordinal);

        foreach (var field in activity.Fields)
        {
            activity.SetValue(field.Name, posted.GetValueOrDefault(field.Name));
        }

        return Edit(activity, context);
    }
}

/// <summary>
/// The editor of a ticket task.
/// </summary>
public class TicketTaskViewModel
{
    public string TicketIdExpression { get; set; }

    public List<TicketTaskFieldViewModel> Fields { get; set; } = [];

    [BindNever]
    public bool NeedsTicket { get; set; }
}

/// <summary>
/// A field of a ticket task in its editor.
/// </summary>
public class TicketTaskFieldViewModel
{
    public string Name { get; set; }

    public string Value { get; set; }

    [BindNever]
    public string Label { get; set; }

    [BindNever]
    public string Hint { get; set; }

    [BindNever]
    public TicketTaskFieldKind Kind { get; set; }

    [BindNever]
    public IList<SelectListItem> Options { get; set; } = [];
}
