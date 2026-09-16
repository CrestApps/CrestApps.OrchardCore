using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "AgentStateReasonCode" recipe step — imports canonical agent state reason codes that map agent-selected reasons to a presence status.
/// </summary>
public sealed class AgentStateReasonCodeRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "AgentStateReasonCode";

    /// <summary>
    /// Builds the JSON schema for this recipe step.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("AgentStateReasonCode").Description("Recipe step discriminator. Must be 'AgentStateReasonCode'.")),
                ("ReasonCodes", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional unique identifier. When supplied and found, the existing reason code is updated instead of a new one being created.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique reason code name shown to agents and supervisors.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Reason code description.")),
                            ("AppliesTo", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Offline", "Available", "Reserved", "Busy", "WrapUp", "Break", "RequestBreak", "Away", "DoNotDisturb", "Meeting", "Training", "AfterHoursUnavailable").Description("Presence state an agent enters when they select this reason code.")),
                            ("SortOrder", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Relative order the reason code is listed in, lowest first.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the reason code can be selected by agents.")))
                        .AdditionalProperties(true))
                    .Description("The agent state reason codes to create or update.")))
            .Required("name", "ReasonCodes")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
