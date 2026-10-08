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
                            ("ItemId", CatalogRecipeStepSchemas.ItemId("channel endpoint")),
                            ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Human-readable name of the address.")),
                            ("AddressType", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Kind of address: 'PhoneNumber' or 'EmailAddress'. When empty, it is taken from 'Channel'.")),
                            ("Capabilities", new JsonSchemaBuilder().Type(SchemaValueType.Array | SchemaValueType.Null).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("What the address is used for, named by channel, for example ['Phone', 'SMS'] for a number used for calls and texts.")),
                            ("MergedItemIds", new JsonSchemaBuilder().Type(SchemaValueType.Array | SchemaValueType.Null).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("Identifiers of records merged into this address; history that names them finds this address.")),
                            ("Channel", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Single channel of an address exported before addresses had capabilities, for example 'SMS' or 'Phone'. Read only when 'AddressType' and 'Capabilities' are absent.")),
                            ("Value", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Address of the endpoint on the channel, for example a phone number or email address.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Administrative description of the channel endpoint.")),
                            ("ProviderName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Technical name of the messaging or telephony provider that owns this address (for example 'Twilio', 'Telnyx' or 'AzureCommunicationServices'). When empty, the tenant-default provider is used.")),
                            ("Properties", CatalogRecipeStepSchemas.Properties("channel endpoint")))
                        .AdditionalProperties(true))
                    .Description("Channel endpoints to create or update.")))
            .Required("name", "ChannelEndpoints")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
