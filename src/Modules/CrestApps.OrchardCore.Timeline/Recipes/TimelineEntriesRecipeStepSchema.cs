using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Timeline.Recipes;

/// <summary>
/// Describes the timeline entries recipe step to CrestApps Recipes.
/// </summary>
internal sealed class TimelineEntriesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => TimelineConstants.RecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(TimelineConstants.RecipeStepName).Description("Recipe step discriminator. Must be 'TimelineEntries'.")),
                (TimelineEntriesRecipeStep.EntriesName, new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(Entry()).Description("The entries to add. An entry whose ItemId is already stored is left as it is.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String).Description(description);

    private static JsonSchemaBuilder Party(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Kind", new JsonSchemaBuilder().Type(SchemaValueType.String).Examples("User", "Contact", "Customer", "Guest").Description("The party kind.")),
                ("Id", Text("The party id.")))
            .Description(description);

    private static JsonSchemaBuilder Entry()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", Text("The entry id; omit for a new entry.")),
                ("Subject", new JsonSchemaBuilder().Type(SchemaValueType.String).Examples("WorkOrder:4abc", "Order:4def").Description("The record the entry is on, as Kind:Id.")),
                ("Kind", new JsonSchemaBuilder().Type(SchemaValueType.String).Examples("Comment", "Note", "Event").Description("What the entry is. Modules add their own kinds.")),
                ("Visibility", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Public", "Internal").Description("Who may read the entry.")),
                ("Author", Party("Who wrote the entry; omit for the site itself.")),
                ("AuthorName", Text("The author's name, for an author no party source knows.")),
                ("Body", Text("The text.")),
                ("Changes", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(("Field", Text("The field.")), ("From", Text("The value before.")), ("To", Text("The value after.")))
                        .Required("Field"))
                    .Description("The field changes the entry records.")),
                ("AttachmentIds", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The ids of its attachments.")),
                ("Channel", Text("The channel it came in or went out on, such as Email.")),
                ("CreatedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.DateTime).Description("When it was written, in UTC.")))
            .Required("Subject", "Kind")
            .AdditionalProperties(true);
    }
}
