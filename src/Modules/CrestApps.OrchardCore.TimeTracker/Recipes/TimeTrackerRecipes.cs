using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.TimeTracker.Core.Services;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using Json.Schema;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Users;
using IClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.TimeTracker.Recipes;

/// <summary>
/// Imports categories (by name), stages (by name, with their categories by name) and entries (by user name and stage
/// name). Existing categories and stages are updated; entries are added unless the same person already has time then.
/// </summary>
internal sealed class TimeTrackerRecipeStep : NamedRecipeStepHandler
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly TimeCategoryManager _categories;
    private readonly TimeStageManager _stages;
    private readonly ITimeTrackerService _tracker;
    private readonly UserManager<IUser> _userManager;

    public TimeTrackerRecipeStep(TimeCategoryManager categories, TimeStageManager stages, ITimeTrackerService tracker, UserManager<IUser> userManager)
        : base(TimeTrackerConstants.RecipeStepName)
    {
        _categories = categories;
        _stages = stages;
        _tracker = tracker;
        _userManager = userManager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var categories = (await _categories.GetAllAsync()).ToList();

        foreach (var node in Items(context, "Categories"))
        {
            var name = Text(node, "name");

            if (string.IsNullOrWhiteSpace(name))
            {
                context.Errors.Add("A category in the TimeTracker step has no name.");

                continue;
            }

            var id = Text(node, "ItemId");
            var category = categories.FirstOrDefault(item => id is not null && item.ItemId == id)
                ?? categories.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            var isNew = category is null;

            category ??= await NewAsync(_categories, id);
            category.Name = name.Trim();
            category.Code = Text(node, "code") ?? category.Code;
            category.Color = Text(node, "color") ?? category.Color;
            category.Position = Number(node, "position") is { } position ? (int)position : category.Position;
            category.Factor = Number(node, "factor") ?? category.Factor;
            category.Enabled = Bool(node, "enabled") ?? category.Enabled;

            if (!await SaveAsync(_categories, category, isNew, context, $"category '{name}'"))
            {
                continue;
            }

            if (isNew)
            {
                categories.Add(category);
            }
        }

        var stages = (await _stages.GetAllAsync()).ToList();

        foreach (var node in Items(context, "Stages"))
        {
            var name = Text(node, "name");

            if (string.IsNullOrWhiteSpace(name))
            {
                context.Errors.Add("A stage in the TimeTracker step has no name.");

                continue;
            }

            var id = Text(node, "ItemId");
            var stage = stages.FirstOrDefault(item => id is not null && item.ItemId == id)
                ?? stages.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            var isNew = stage is null;

            stage ??= await NewAsync(_stages, id);
            stage.Name = name.Trim();
            stage.Code = Text(node, "code") ?? stage.Code;
            stage.Color = Text(node, "color") ?? stage.Color;
            stage.Position = Number(node, "position") is { } position ? (int)position : stage.Position;
            stage.SelfService = Bool(node, "selfService") ?? stage.SelfService;
            stage.Enabled = Bool(node, "enabled") ?? stage.Enabled;

            if (node["categories"] is JsonArray names)
            {
                stage.CategoryIds = [];

                foreach (var categoryName in names.Select(item => item?.GetValue<string>()).Where(item => !string.IsNullOrWhiteSpace(item)))
                {
                    var category = categories.FirstOrDefault(item => string.Equals(item.Name, categoryName, StringComparison.OrdinalIgnoreCase));

                    if (category is null)
                    {
                        context.Errors.Add($"The stage '{name}' in the TimeTracker step counts toward the category '{categoryName}', which doesn't exist.");

                        continue;
                    }

                    stage.CategoryIds.Add(category.ItemId);
                }
            }

            if (await SaveAsync(_stages, stage, isNew, context, $"stage '{name}'") && isNew)
            {
                stages.Add(stage);
            }
        }

        foreach (var node in Items(context, "Entries"))
        {
            var userName = Text(node, "userName");
            var stageName = Text(node, "stage");
            var user = string.IsNullOrWhiteSpace(userName) ? null : await _userManager.FindByNameAsync(userName);
            var stage = stages.FirstOrDefault(item => string.Equals(item.Name, stageName, StringComparison.OrdinalIgnoreCase));

            if (user is null || stage is null || node["startUtc"] is null || node["endUtc"] is null)
            {
                context.Errors.Add($"An entry in the TimeTracker step needs a user that exists ('{userName}'), a stage that exists ('{stageName}'), a startUtc and an endUtc.");

                continue;
            }

            var entry = new TimeEntry
            {
                UserId = await _userManager.GetUserIdAsync(user),
                UserName = user.UserName,
                StageId = stage.ItemId,
                StartUtc = node["startUtc"].GetValue<DateTime>().ToUniversalTime(),
                EndUtc = node["endUtc"].GetValue<DateTime>().ToUniversalTime(),
                Source = Text(node, "source") ?? TimeTrackerConstants.Sources.Import,
                Note = Text(node, "note"),
            };
            var existing = await _tracker.ListAsync(new TimeEntryQuery { UserIds = [entry.UserId], FromUtc = entry.StartUtc, ToUtc = entry.EndUtc });

            if (existing.Count > 0)
            {
                continue;
            }

            var added = await _tracker.AddAsync(entry, "Recipe", null);

            foreach (var error in added.Errors)
            {
                context.Errors.Add($"An entry of '{userName}' in the TimeTracker step wasn't added: {error}");
            }
        }
    }

    // A new record keeps the id it had where it was exported, so records that point at it, such as activity codes,
    // still do.
    private static async Task<T> NewAsync<T>(CrestApps.Core.Services.ICatalogManager<T> manager, string id)
        where T : CrestApps.Core.Models.CatalogItem
    {
        var item = await manager.NewAsync();

        if (!string.IsNullOrEmpty(id) && CrestApps.Core.UniqueId.IsValid(id))
        {
            item.ItemId = id;
        }

        return item;
    }

    private static IEnumerable<JsonObject> Items(RecipeExecutionContext context, string section)
        => (context.Step[section] as JsonArray ?? []).OfType<JsonObject>();

    private static string Text(JsonObject node, string name)
        => node[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;

    private static decimal? Number(JsonObject node, string name)
        => node[name] is JsonValue value && value.TryGetValue<decimal>(out var number) ? number : null;

    private static bool? Bool(JsonObject node, string name)
        => node[name] is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;

    private static async Task<bool> SaveAsync<T>(CrestApps.Core.Services.ICatalogManager<T> manager, T item, bool isNew, RecipeExecutionContext context, string label)
        where T : CrestApps.Core.Models.CatalogItem
    {
        var validation = await manager.ValidateAsync(item);

        if (!validation.Succeeded)
        {
            context.Errors.Add($"The {label} in the TimeTracker step wasn't saved: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}");

            return false;
        }

        if (isNew)
        {
            await manager.CreateAsync(item);
        }
        else
        {
            await manager.UpdateAsync(item);
        }

        return true;
    }
}

/// <summary>
/// Describes the TimeTracker recipe step.
/// </summary>
internal sealed class TimeTrackerRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => TimeTrackerConstants.RecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Creates or updates time categories and stages by name, and adds time entries.")
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(TimeTrackerConstants.RecipeStepName).Description("Recipe step discriminator. Must be 'TimeTracker'.")),
                ("Categories", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description("Categories, matched by ItemId and then name: name, code, color, position, factor (how a minute counts, 1 by default) and enabled.")),
                ("Stages", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description("Stages, matched by ItemId and then name: name, code, color, position, selfService, enabled, and categories, the names of the categories its time counts toward.")),
                ("Entries", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description("Entries: userName, stage (a stage name), startUtc, endUtc, source and note. An entry is skipped when the person already has time then.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}

/// <summary>
/// Exports the categories and stages, and the entries of the last days when asked.
/// </summary>
public sealed class TimeTrackerDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TimeTrackerDeploymentStep>("Time Tracker");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TimeTrackerDeploymentStep>("Time stages, categories and entries");

    /// <inheritdoc/>
    public override string Name => TimeTrackerConstants.RecipeStepName;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets whether the entries are exported too.
    /// </summary>
    public bool IncludeEntries { get; set; }

    /// <summary>
    /// Gets or sets how many days back entries are exported.
    /// </summary>
    public int Days { get; set; } = 31;
}

internal sealed class TimeTrackerDeploymentSource : DeploymentSourceBase<TimeTrackerDeploymentStep>
{
    private readonly TimeCategoryManager _categories;
    private readonly TimeStageManager _stages;
    private readonly ITimeTrackerService _tracker;
    private readonly IClock _clock;

    public TimeTrackerDeploymentSource(TimeCategoryManager categories, TimeStageManager stages, ITimeTrackerService tracker, IClock clock)
    {
        _categories = categories;
        _stages = stages;
        _tracker = tracker;
        _clock = clock;
    }

    protected override async Task ProcessAsync(TimeTrackerDeploymentStep step, DeploymentPlanResult result)
    {
        var categories = (await _categories.GetAllAsync()).ToList();
        var categoryNames = categories.ToDictionary(item => item.ItemId, item => item.Name, StringComparer.Ordinal);
        var stages = (await _stages.GetAllAsync()).ToList();
        var stageNames = stages.ToDictionary(item => item.ItemId, item => item.Name, StringComparer.Ordinal);
        var node = new JsonObject
        {
            ["name"] = step.Name,
            ["Categories"] = new JsonArray([.. categories.Select(item => new JsonObject
            {
                ["ItemId"] = item.ItemId,
                ["name"] = item.Name,
                ["code"] = item.Code,
                ["color"] = item.Color,
                ["position"] = item.Position,
                ["factor"] = item.Factor,
                ["enabled"] = item.Enabled,
            })]),
            ["Stages"] = new JsonArray([.. stages.Select(item => new JsonObject
            {
                ["ItemId"] = item.ItemId,
                ["name"] = item.Name,
                ["code"] = item.Code,
                ["color"] = item.Color,
                ["position"] = item.Position,
                ["selfService"] = item.SelfService,
                ["enabled"] = item.Enabled,
                ["categories"] = new JsonArray([.. item.CategoryIds.Where(categoryNames.ContainsKey).Select(id => (JsonNode)categoryNames[id])]),
            })]),
        };

        if (step.IncludeEntries)
        {
            var now = _clock.UtcNow;
            var entries = await _tracker.ListAsync(new TimeEntryQuery { FromUtc = now.AddDays(-Math.Clamp(step.Days, 1, 3660)), ToUtc = now });

            node["Entries"] = new JsonArray([.. entries.Where(entry => entry.EndUtc is not null && entry.StageId is not null && stageNames.ContainsKey(entry.StageId)).Select(entry => new JsonObject
            {
                ["userName"] = entry.UserName,
                ["stage"] = stageNames[entry.StageId],
                ["startUtc"] = entry.StartUtc,
                ["endUtc"] = entry.EndUtc,
                ["source"] = entry.Source,
                ["note"] = entry.Note,
            })]);
        }

        result.Steps.Add(node);
    }
}

/// <summary>
/// The deployment step's settings form.
/// </summary>
public class TimeTrackerDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets whether the entries are exported too.
    /// </summary>
    public bool IncludeEntries { get; set; }

    /// <summary>
    /// Gets or sets how many days back entries are exported.
    /// </summary>
    public int Days { get; set; } = 31;
}

internal sealed class TimeTrackerDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TimeTrackerDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(TimeTrackerDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("TimeTrackerDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TimeTrackerDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(TimeTrackerDeploymentStep step, BuildEditorContext context)
        => Initialize<TimeTrackerDeploymentStepViewModel>("TimeTrackerDeploymentStep_Fields_Edit", model =>
        {
            model.IncludeEntries = step.IncludeEntries;
            model.Days = step.Days;
        }).Location("Content");

    public override async Task<IDisplayResult> UpdateAsync(TimeTrackerDeploymentStep step, UpdateEditorContext context)
    {
        var model = new TimeTrackerDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        step.IncludeEntries = model.IncludeEntries;
        step.Days = Math.Clamp(model.Days, 1, 3660);

        return Edit(step, context);
    }
}

/// <summary>
/// Describes the TimeTracker deployment step.
/// </summary>
internal sealed class TimeTrackerDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(TimeTrackerDeploymentStep);

    protected override string DisplayText => "Time stages, categories and entries";

    protected override string Description => "Exports the time categories and stages, and optionally the entries of the last days.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeEntries", DeploymentSchemaBuilders.Boolean("When true, exports the time entries too."));
        yield return ("Days", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Maximum(3660).Description("How many days back entries are exported."));
    }
}
