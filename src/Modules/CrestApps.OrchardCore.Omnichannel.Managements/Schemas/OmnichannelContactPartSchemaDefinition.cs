using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using Json.Schema;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Schemas;

/// <summary>
/// Provides recipe schema support for <see cref="OmnichannelContactPart"/>.
/// </summary>
public sealed class OmnichannelContactPartSchemaDefinition : PartSchemaDefinitionBase
{
    private readonly ITimeZoneSelectListProvider _timeZoneSelectListProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactPartSchemaDefinition"/> class.
    /// </summary>
    /// <param name="timeZoneSelectListProvider">The time zone provider used to populate the <c>TimeZoneId</c> enum.</param>
    public OmnichannelContactPartSchemaDefinition(ITimeZoneSelectListProvider timeZoneSelectListProvider)
    {
        _timeZoneSelectListProvider = timeZoneSelectListProvider;
    }

    /// <inheritdoc />
    public override string Name => OmnichannelConstants.ContentParts.OmnichannelContact;

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("OmnichannelContactPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("RequireTimeZone", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                            .Default(true)
                            .Description("Require a time zone for the contact.")),
                        ("UseDoNotCall", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                            .Default(true)
                            .Description("Show the Do not call preference.")),
                        ("UseDoNotSms", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                            .Description("Show the Do not SMS preference.")),
                        ("UseDoNotChat", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                            .Description("Show the Do not chat preference.")),
                        ("UseDoNotEmail", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                            .Description("Show the Do not email preference.")))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);

    protected override async ValueTask<JsonSchemaBuilder> BuildPartSchemaAsync(
        ContentPartSchemaContext context,
        CancellationToken cancellationToken = default)
    {
        var timeZoneIds = (await _timeZoneSelectListProvider.GetTimeZoneSelectListAsync(cancellationToken))
            .Select(item => item.Key)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var timeZoneIdSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("The contact's IANA time zone identifier.");

        if (timeZoneIds.Length > 0)
        {
            timeZoneIdSchema = timeZoneIdSchema.Enum(timeZoneIds);
        }

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("TimeZoneId", timeZoneIdSchema),
                ("DoNotCall", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Description("Whether phone calls are blocked for the contact.")),
                ("DoNotCallUtc", CreateUtcDateTimeSchema("The UTC timestamp when phone calls were blocked for the contact.")),
                ("DoNotEmail", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Description("Whether email is blocked for the contact.")),
                ("DoNotEmailUtc", CreateUtcDateTimeSchema("The UTC timestamp when email was blocked for the contact.")),
                ("DoNotSms", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Description("Whether SMS is blocked for the contact.")),
                ("DoNotSmsUtc", CreateUtcDateTimeSchema("The UTC timestamp when SMS was blocked for the contact.")),
                ("DoNotChat", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Description("Whether chat is blocked for the contact.")),
                ("DoNotChatUtc", CreateUtcDateTimeSchema("The UTC timestamp when chat was blocked for the contact.")))
            .AdditionalProperties(true);
    }

    private static JsonSchemaBuilder CreateUtcDateTimeSchema(string description)
        => new JsonSchemaBuilder().AnyOf(
            new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .Description(description),
            new JsonSchemaBuilder().Type(SchemaValueType.Null));
}
