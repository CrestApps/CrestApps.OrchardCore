using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Learning.Packages.Recipes;

/// <summary>
/// Imports learners' runtime states on learning packages. People are matched by user name; a row whose learner is not
/// on this site is reported as a recipe error and skipped. A state keeps its id, and one enrollment has one state per
/// package, so a second import updates it. The package files are not in the step: they stay in the protected store of
/// the site they were imported on.
/// </summary>
internal sealed class LearningPackageRuntimeStatesRecipeStep : NamedRecipeStepHandler
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly PackageRuntimeStore _store;
    private readonly LearnerUserLookup _userLookup;

    internal readonly IStringLocalizer S;

    public LearningPackageRuntimeStatesRecipeStep(PackageRuntimeStore store, LearnerUserLookup userLookup, IStringLocalizer<LearningPackageRuntimeStatesRecipeStep> stringLocalizer)
        : base(PackagesConstants.RuntimeStatesRecipeStep)
    {
        _store = store;
        _userLookup = userLookup;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step["States"] is not JsonArray rows)
        {
            return;
        }

        foreach (var row in rows.OfType<JsonObject>())
        {
            var userName = row["UserName"]?.GetValue<string>();
            var userId = await _userLookup.FindUserIdAsync(userName);
            var label = row["ItemId"]?.GetValue<string>() ?? row["PackageItemId"]?.GetValue<string>();

            if (userId is null)
            {
                context.Errors.Add(S["The user '{0}' of runtime state {1} is not on this site; it was skipped.", userName, label]);

                continue;
            }

            PackageRuntimeState incoming;

            try
            {
                incoming = row.Deserialize<PackageRuntimeState>(JsonOptions);
            }
            catch (JsonException exception)
            {
                context.Errors.Add(S["Runtime state {0} could not be read: {1}", label, exception.Message]);

                continue;
            }

            if (string.IsNullOrWhiteSpace(incoming.EnrollmentId) || string.IsNullOrWhiteSpace(incoming.PackageItemId))
            {
                context.Errors.Add(S["Runtime state {0} has no enrollment or no package; it was skipped.", label]);

                continue;
            }

            incoming.UserId = userId;
            incoming.Cmi ??= new Scorm12Cmi();

            // An imported state never carries a launch: the next launch on this site starts a session.
            incoming.CurrentLaunchId = null;

            var existing = (string.IsNullOrEmpty(incoming.ItemId) ? null : await _store.FindByIdAsync(incoming.ItemId))
                ?? await _store.FindAsync(incoming.EnrollmentId, incoming.PackageItemId);

            if (existing is null)
            {
                incoming.ItemId = string.IsNullOrEmpty(incoming.ItemId) ? global::OrchardCore.IdGenerator.GenerateId() : incoming.ItemId;
                await _store.CreateAsync(incoming);

                continue;
            }

            // Copied onto the instance the session tracks; saving the deserialized copy would store a second document.
            var itemId = existing.ItemId;

            foreach (var property in typeof(PackageRuntimeState).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    property.SetValue(existing, property.GetValue(incoming));
                }
            }

            existing.ItemId = itemId;
            await _store.UpdateAsync(existing);
        }
    }
}

/// <summary>
/// Describes the LearningPackageRuntimeStates recipe step to CrestApps Recipes.
/// </summary>
internal sealed class LearningPackageRuntimeStatesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => PackagesConstants.RuntimeStatesRecipeStep;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(PackagesConstants.RuntimeStatesRecipeStep).Description("Recipe step discriminator. Must be 'LearningPackageRuntimeStates'.")),
                ("States", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("ItemId", Text("The runtime state id, a generated unique id. A state with this id, or of the same enrollment and package, is updated.")),
                        ("UserName", Text("The user name of the learner. A row whose user is not on the site is reported and skipped.")),
                        ("EnrollmentId", Text("The enrollment.")),
                        ("CourseId", Text("The content item id of the course.")),
                        ("PackageItemId", Text("The content item id of the learning package.")),
                        ("PackageTitle", Text("The title of the package at the last launch.")),
                        ("PackageFileId", Text("The package file of the last launch, in the protected store of the site it came from.")),
                        ("Cmi", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("The SCORM 1.2 data: LessonStatus, ScoreRaw, ScoreMin, ScoreMax, TotalTime, LessonLocation, SuspendData, Exit, Entry, Comments, the student preferences, Objectives and Interactions.")),
                        ("Sessions", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("How many times the learner launched the package.")),
                        ("FirstLaunchedUtc", Text("When the package was first launched, in UTC.")),
                        ("LastLaunchedUtc", Text("When the package was last launched, in UTC.")),
                        ("LastCommitUtc", Text("When the content last committed its data, in UTC.")),
                        ("LastFinishedUtc", Text("When a session last finished, in UTC.")))
                    .Required("UserName", "EnrollmentId", "PackageItemId")).Description("The runtime states to create, or update.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String).Description(description);
}
