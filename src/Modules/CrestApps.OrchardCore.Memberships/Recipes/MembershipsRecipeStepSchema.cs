using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Memberships.Recipes;

/// <summary>
/// Describes the <c>Memberships</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class MembershipsRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => MembershipsConstants.Recipes.Memberships;

    protected override string EntriesName => MembershipsStep.EntriesName;

    protected override string Description => "Creates or updates memberships, matched by ItemId, then by member, product and start. The member is named by UserName and must exist on the site. An active membership grants its roles.";

    protected override string EntryDescription => "A member's membership from a membership product.";

    protected override IEnumerable<string> RequiredEntryProperties => ["UserName", "ContentItemId", "StartsUtc"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("UserName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The member's user name. The user must exist on the site.")),
            ("ContentItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The content item id of the membership product.")),
            ("Title", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The membership's title, such as the product's.")),
            ("RoleNames", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The roles the membership gives its member while it is active.")),
            ("OrderIds", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The orders that bought or renewed the membership.")),
            ("StartsUtc", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("When the membership starts.")),
            ("EndsUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("When the membership ends, or none for one that doesn't.")),
            ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Integer).Description("The membership's status.")),
        ];
}
