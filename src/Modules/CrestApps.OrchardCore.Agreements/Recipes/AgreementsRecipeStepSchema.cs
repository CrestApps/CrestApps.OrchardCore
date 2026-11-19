using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Agreements.Recipes;

/// <summary>
/// The schema of the <c>Agreements</c> recipe step.
/// </summary>
internal sealed class AgreementsRecipeStepSchema : RecipeStepSchemaBase
{
    /// <inheritdoc/>
    public override string Name => AgreementsConstants.RecipeStepName;

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                (AgreementsRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The agreement id; omit it to create one.")),
                            ("Number", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The number; omit it to number a new agreement.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The name.")),
                            ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Draft", "Active", "Paused", "Expired", "Canceled").Description("Where it stands.")),
                            ("AccountId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The account it is with.")),
                            ("StartDate", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Date).Description("Its first day.")),
                            ("EndDate", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.Date).Description("Its last day; empty when it runs until canceled.")),
                            ("TimeZoneId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The time zone its days and months are counted in.")),
                            ("Hours", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object | SchemaValueType.Null)
                                .Properties(
                                    ("Kind", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("None", "Block", "MonthlyAllowance").Description("How it covers hours.")),
                                    ("Hours", new JsonSchemaBuilder().Type(SchemaValueType.Number).Minimum(0).Description("The hours of the block, or of each month.")),
                                    ("RoundingMinutes", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Enum(0, 6, 15, 30).Description("What each entry of time is rounded up to, in minutes.")))
                                .Description("The hours it covers.")),
                            ("ExpiringNoticeDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Maximum(365).Description("Days before its end it is told to be about to end.")),
                            ("Notes", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Notes for the people who work under it.")),
                            ("Properties", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).Description("The terms other modules keep, by name.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The agreements to create or update by id.")),
            ],
            [AgreementsRecipeStep.EntriesName]);
}
