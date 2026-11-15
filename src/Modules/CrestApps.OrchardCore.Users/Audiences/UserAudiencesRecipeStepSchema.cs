using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Users.Audiences;

/// <summary>
/// Describes the UserAudiences recipe step.
/// </summary>
internal sealed class UserAudiencesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => UsersConstants.AudiencesRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        var rule = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("source", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The rule source, such as Role, Team, StaffHireDate, StaffEmploymentType, SkillHolder, CertificationHolder or CourseLearner.")),
                ("values", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The values chosen, such as role names or team ids; a person matches any of them.")),
                ("number", new JsonSchemaBuilder().Type(SchemaValueType.Integer, SchemaValueType.Null).Description("A number the source asks for, such as days since hire.")))
            .Required("source");

        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(UsersConstants.AudiencesRecipeStepName).Description("Recipe step discriminator. Must be 'UserAudiences'.")),
                ("Audiences", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The technical name: letters, digits, dots, dashes and underscores.")),
                        ("displayName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name shown to people.")),
                        ("description", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A description.")),
                        ("join", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Integer).Description("All (0): a person matches every rule; Any (1): one rule.")),
                        ("rules", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(rule).Description("The rules.")))
                    .Required("name", "rules")).Description("The audiences to create, or update by name.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
