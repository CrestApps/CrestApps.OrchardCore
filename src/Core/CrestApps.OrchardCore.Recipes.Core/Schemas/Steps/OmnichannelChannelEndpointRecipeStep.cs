using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "OmnichannelChannelEndpoint" recipe step — imports the endpoints used to reach contacts on a channel.
/// </summary>
public sealed class OmnichannelChannelEndpointRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "OmnichannelChannelEndpoint";

    /// <summary>
    /// Builds the JSON schema for this recipe step.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("OmnichannelChannelEndpoint").Description("Recipe step discriminator. Must be 'OmnichannelChannelEndpoint'.")),
                ("ChannelEndpoints", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Stable identifier of the channel endpoint. When it matches an existing endpoint the entry is updated; otherwise a new endpoint is created.")),
                            ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Human-readable name of the channel endpoint.")),
                            ("Channel", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Communication channel this endpoint belongs to, for example 'SMS', 'Chat', or 'Email'.")),
                            ("Value", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Address of the endpoint on the channel, for example a phone number or email address.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Administrative description of the channel endpoint.")))
                        .AdditionalProperties(true))
                    .Description("Channel endpoints to create or update.")))
            .Required("name", "ChannelEndpoints")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
